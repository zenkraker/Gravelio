using System.Security.Cryptography;
using System.Text;

namespace Gravelio.Api.Auth;

public static class PasswordReset
{
    public const int LifetimeMinutes = 60;

    /// A URL-safe 256-bit token. The database keeps only its hash, so a leaked table cannot be used to reset a password.
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
