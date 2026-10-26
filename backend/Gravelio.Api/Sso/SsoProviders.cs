using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Gravelio.Api.Sso;

/// An enabled provider as the authentication handler sees it, with its secret in the clear (memory only).
public sealed record SsoProviderConfig(int Id, string Name, string Slug, string Issuer, string ClientId, string? ClientSecret, string Scopes, bool AutoCreateUsers);

/// Providers live in the database, so their authentication schemes are added and removed at run time. The handler does the protocol work
/// (state, nonce, PKCE, discovery, token validation); this class only tells it, per scheme, what the admin configured, which is the
/// pattern for dynamic schemes (<see cref="IAuthenticationSchemeProvider.AddScheme"/>, https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.iauthenticationschemeprovider.addscheme), and the options cache
/// entry is dropped whenever a provider changes, so the next request builds fresh options.
public sealed class SsoProviders(IServiceScopeFactory scopes, IAuthenticationSchemeProvider schemes, IOptionsMonitorCache<OpenIdConnectOptions> cache,
    IServiceProvider services,
    IDataProtectionProvider protection, IOptions<OutboundOptions> outbound, IOptions<AuthOptions> auth, ILogger<SsoProviders> log)
    : IConfigureNamedOptions<OpenIdConnectOptions>, IHostedService
{
    const string Purpose = "Gravelio.SsoClientSecret";

    volatile Dictionary<string, SsoProviderConfig> current = [];

    public static string SchemeName(string slug) => $"sso-{slug}";

    public SsoProviderConfig? Find(string slug) => current.GetValueOrDefault(SchemeName(slug));

    /// What is stored for a client secret: protected with the key ring on the keys volume, never readable from a database dump.
    public static string Protect(IDataProtectionProvider protection, string secret) => protection.CreateProtector(Purpose).Protect(secret);

    /// Reloads the enabled providers from the database: schemes of removed or disabled ones go, new ones come, changed ones are rebuilt.
    public async Task RefreshAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDb>().IdentityProviders.AsNoTracking().Where(p => p.Enabled).ToListAsync(ct);
        var protector = protection.CreateProtector(Purpose);
        var next = new Dictionary<string, SsoProviderConfig>();
        foreach (var r in rows)
        {
            try
            {
                next[SchemeName(r.Slug)] = new SsoProviderConfig(r.Id, r.Name, r.Slug, r.Issuer, r.ClientId, r.ClientSecret is null ? null : protector.Unprotect(r.ClientSecret), r.Scopes, r.AutoCreateUsers);
            }
            catch (System.Security.Cryptography.CryptographicException e)
            {
                // The key ring that protected the secret is gone: the provider cannot work until its secret is entered again.
                log.LogError(e, "The client secret of sign-in provider {Slug} cannot be read; enter it again", r.Slug);
            }
        }

        var previous = current;
        current = next;
        // The authentication middleware builds every provider's handler on every request, so one provider whose options cannot be built
        // (an http issuer on a server that forbids it, say) would answer 500 for the whole site. Such a provider is left out instead.
        foreach (var name in next.Keys.ToList())
        {
            try
            {
                cache.TryRemove(name);
                // Resolved here, not in the constructor: the factory depends on this class (it is one of the option set-ups), which would be a cycle.
                services.GetRequiredService<IOptionsFactory<OpenIdConnectOptions>>().Create(name);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or UriFormatException)
            {
                log.LogError(e, "Sign-in provider {Name} is not usable and is left out: {Reason}", name, e.Message);
                next.Remove(name);
            }
        }
        foreach (var name in previous.Keys.Where(n => !next.ContainsKey(n)))
        {
            schemes.RemoveScheme(name);
            cache.TryRemove(name);
        }
        foreach (var (name, config) in next)
        {
            if (previous.TryGetValue(name, out var before) && before == config) continue;
            if (await schemes.GetSchemeAsync(name) is null) schemes.AddScheme(new AuthenticationScheme(name, config.Name, typeof(OpenIdConnectHandler)));
            cache.TryRemove(name);
        }
    }

    public void Configure(string? name, OpenIdConnectOptions o)
    {
        if (name is null || !current.TryGetValue(name, out var p)) return;
        var allowPrivate = outbound.Value.AllowPrivateNetworks;
        o.Authority = p.Issuer;
        o.ClientId = p.ClientId;
        o.ClientSecret = p.ClientSecret;
        o.ResponseType = OpenIdConnectResponseType.Code; // the authorization code flow, with PKCE
        o.ResponseMode = OpenIdConnectResponseMode.Query; // a plain redirect back, so the Lax correlation cookies below are sent
        o.UsePkce = true;
        o.Scope.Clear();
        foreach (var scope in p.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) o.Scope.Add(scope);
        o.CallbackPath = $"/api/v1/auth/sso/{p.Slug}/callback";
        o.SignInScheme = IdentityConstants.ExternalScheme;
        o.SaveTokens = false;
        o.GetClaimsFromUserInfoEndpoint = true;
        o.MapInboundClaims = false; // keep the provider's own names: sub, email, email_verified, preferred_username
        o.ClaimActions.MapUniqueJsonKey("email_verified", "email_verified");
        o.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");
        o.RequireHttpsMetadata = !allowPrivate;
        // Discovery, the token endpoint and the user-info endpoint are fetched through the guarded handler (no private addresses, https only).
        o.BackchannelHttpHandler = SafeHttp.CreateHandler(allowPrivate);
        var secure = auth.Value.SecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        foreach (var cookie in new[] { o.CorrelationCookie, o.NonceCookie })
        {
            cookie.SameSite = SameSiteMode.Lax;
            cookie.SecurePolicy = secure;
        }
        o.Events.OnRemoteFailure = ctx =>
        {
            ctx.Response.Redirect("/login?sso_error=failed");
            ctx.HandleResponse();
            return Task.CompletedTask;
        };
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await RefreshAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // A server whose database is not reachable yet still starts; the providers load on the first admin change.
            log.LogError(e, "Could not load the sign-in providers");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
