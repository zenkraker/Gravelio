using FastEndpoints;
using Gravelio.Api.Common;
using Microsoft.AspNetCore.Authentication;

namespace Gravelio.Api.Sso;

/// Sends the browser to the identity provider. The handler adds the state, nonce and PKCE challenge; the way back ends at
/// <c>/auth/sso/{slug}/complete</c>, where Gravelio decides who has signed in.
public sealed class StartSsoLoginEndpoint(SsoProviders sso, ILogger<StartSsoLoginEndpoint> log) : Endpoint<StartSsoLoginRequest>
{
    public override void Configure()
    {
        Get("/auth/sso/{slug}/login");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(StartSsoLoginRequest req, CancellationToken ct)
    {
        if (sso.Find(req.Slug) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var props = new AuthenticationProperties { RedirectUri = $"/api/v1/auth/sso/{req.Slug}/complete" };
        props.Items["sso_provider"] = req.Slug;
        try
        {
            await Send.ResultAsync(TypedResults.Challenge(props, [SsoProviders.SchemeName(req.Slug)]));
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or IOException)
        {
            // The provider's discovery document could not be fetched or read (down, wrong address, a private address refused).
            log.LogWarning(e, "Could not start sign-in with provider {Slug}", req.Slug);
            await Send.RedirectAsync("/login?sso_error=unavailable", allowRemoteRedirects: false);
        }
    }
}
