using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.ApiKeys;

public static class ApiKeyAuth
{
    public const string Scheme = "ApiKey";
    public const string Header = "X-API-Key";
    public const string Prefix = "gravelio_";
    public const int MaxKeysPerUser = 20;

    /// A new key: 256 random bits, so a fast hash is enough to store it (there is nothing to guess or brute-force).
    public static (string Key, string Prefix, string Hash) Generate()
    {
        var key = Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (key, key[..13], Hash(key));
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}

/// Marks an endpoint that also accepts an API key, so the antiforgery check can step aside for a request that carries a key and no cookie.
public sealed record AllowsApiKey;

/// Signs a request in from the X-API-Key header. Only endpoints that list the scheme accept it; the key never works as a login
/// and is not read from the query string, where it would end up in logs.
public sealed class ApiKeyHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AppDb db, TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    static readonly TimeSpan LastUsedGranularity = TimeSpan.FromHours(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuth.Header, out var values) || values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            return AuthenticateResult.NoResult();

        var hash = ApiKeyAuth.Hash(values[0]!);
        var now = time.GetUtcNow().UtcDateTime;
        var key = await db.ApiKeys.AsNoTracking()
            .Where(k => k.KeyHash == hash && k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now) && k.User.Active)
            .Select(k => new { k.Id, k.UserId, k.User.UserName, k.LastUsedAt })
            .FirstOrDefaultAsync(Context.RequestAborted);
        if (key is null) return AuthenticateResult.Fail("Invalid API key");

        // Written at most once an hour, so a busy sync does not turn every upload into two writes.
        if (key.LastUsedAt is null || now - key.LastUsedAt > LastUsedGranularity)
            await db.ApiKeys.Where(k => k.Id == key.Id).ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now), Context.RequestAborted);

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, key.UserId.ToString()), new Claim(ClaimTypes.Name, key.UserName!)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
