using FastEndpoints;
using Gravelio.Api.Auth;
using Microsoft.AspNetCore.Authentication;

namespace Gravelio.Api.Sso;

/// Connects the signed-in account to an identity provider: the person is sent to sign in there, and on the way back that identity is
/// added to this account.
public sealed class StartSsoLinkEndpoint(SsoProviders sso, ILogger<StartSsoLinkEndpoint> log) : Endpoint<StartSsoLinkRequest>
{
    public override void Configure() => Get("/users/me/sso/{slug}/link");

    public override async Task HandleAsync(StartSsoLinkRequest req, CancellationToken ct)
    {
        if (sso.Find(req.Slug) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var props = new AuthenticationProperties { RedirectUri = $"/api/v1/auth/sso/{req.Slug}/complete" };
        props.Items["sso_provider"] = req.Slug;
        props.Items["sso_link_user"] = User.UserId().ToString();
        try
        {
            await Send.ResultAsync(TypedResults.Challenge(props, [SsoProviders.SchemeName(req.Slug)]));
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or IOException)
        {
            // The provider's discovery document could not be fetched or read (down, wrong address, a private address refused).
            log.LogWarning(e, "Could not start sign-in with provider {Slug}", req.Slug);
            await Send.RedirectAsync("/security?sso_error=unavailable", allowRemoteRedirects: false);
        }
    }
}
