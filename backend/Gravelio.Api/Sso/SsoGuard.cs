using Gravelio.Api.Common;

namespace Gravelio.Api.Sso;

public static class SsoGuard
{
    /// An issuer is a URL an admin typed in and the server will fetch, so it follows the outbound rules: https, unless private networks are
    /// allowed. (Where it points is checked again at connect time by <see cref="SafeHttp"/>.)
    public static bool IsSafeIssuer(string issuer, OutboundOptions outbound) =>
        outbound.AllowPrivateNetworks || issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public const string HttpsRequired = "The issuer address must use https.";
}
