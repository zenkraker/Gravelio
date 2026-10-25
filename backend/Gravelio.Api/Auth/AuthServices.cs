using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Gravelio.Api.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Gravelio.Api.Auth;

public class AuthOptions
{
    public string JwtSecret { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public bool SecureCookies { get; set; } = true;
}

public class TokenService(AuthOptions options)
{
    public const string Issuer = "gravelio";
    public const string SessionClaim = "sid";

    public SymmetricSecurityKey Key { get; } = new(Encoding.UTF8.GetBytes(options.JwtSecret));

    public (string Token, int ExpiresIn) CreateAccessToken(User user, string sessionId)
    {
        var expires = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims: [new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(SessionClaim, sessionId), new(ClaimTypes.Role, user.AccessType)],
            expires: expires,
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), options.AccessTokenMinutes * 60);
    }

    /// Opaque refresh token "{sessionId}.{secret}"; only the SHA-256 of the whole token is stored.
    public static string NewRefreshToken(string sessionId) =>
        $"{sessionId}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))}";

    // A fast hash is fine here: the token carries 256 bits of randomness, unlike a password.
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool HashEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}

/// Progressive per-username lockout: 5 failures → 5 min, 10 → 30 min, 20 → 24 h.
// shortcut: in-process memory, so counts reset on restart and aren't shared across nodes; move to Redis/DB if you scale out.
public class LoginLockout(IMemoryCache cache)
{
    record Entry(int Failures, DateTime? LockedUntil);

    static string Key(string username) => "lockout:" + username.ToLowerInvariant();

    public TimeSpan? Remaining(string username) =>
        cache.Get<Entry>(Key(username))?.LockedUntil is { } until && until > DateTime.UtcNow ? until - DateTime.UtcNow : null;

    public void RecordFailure(string username)
    {
        var failures = (cache.Get<Entry>(Key(username))?.Failures ?? 0) + 1;
        TimeSpan? lockFor = failures switch { >= 20 => TimeSpan.FromHours(24), >= 10 => TimeSpan.FromMinutes(30), >= 5 => TimeSpan.FromMinutes(5), _ => null };
        cache.Set(Key(username), new Entry(failures, DateTime.UtcNow + lockFor), TimeSpan.FromHours(24));
    }

    public void Reset(string username) => cache.Remove(Key(username));
}

public static class PasswordPolicy
{
    const string Punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    /// Returns an error message, or null when the password is acceptable for this access type.
    public static string? Validate(string password, string accessType)
    {
        int min = accessType == AccessTypes.Admin ? 12 : 8;
        if (password.Length < min) return $"Password must be at least {min} characters long.";
        if (password.Length > 128) return "Password must be at most 128 characters long.";
        if (!password.Any(char.IsUpper)) return "Password must contain at least one uppercase letter.";
        if (!password.Any(char.IsLower)) return "Password must contain at least one lowercase letter.";
        if (!password.Any(char.IsDigit)) return "Password must contain at least one digit.";
        if (!password.Any(Punctuation.Contains)) return $"Password must contain at least one special character ({Punctuation}).";
        return null;
    }
}
