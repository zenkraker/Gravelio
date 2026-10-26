using System.Text.RegularExpressions;
using FluentValidation;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sso;

public static partial class SsoRules
{
    public const string DefaultScopes = "openid profile email";

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,38}[a-z0-9])?$")]
    public static partial Regex SlugPattern();

    public static IRuleBuilderOptions<T, string> ValidUrl<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => Uri.TryCreate(v, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment))
            .WithMessage("Enter the provider's issuer address, for example https://auth.example.com/realms/home.");

    public static IRuleBuilderOptions<T, string?> ValidScopes<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is null || v.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid")).WithMessage("The scopes must include openid.");

    /// Password sign-in may only be turned off while somebody can still sign in another way. True when that is the case.
    public static async Task<bool> LocalLoginMayBeOffAsync(AppDb db, CancellationToken ct) => await db.IdentityProviders.AnyAsync(p => p.Enabled, ct);
}
