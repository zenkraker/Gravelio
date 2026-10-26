using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Settings;

/// Everything under /server-settings that only admins may call.
public sealed class AdminSettingsGroup : Group
{
    public AdminSettingsGroup() => Configure("server-settings", ep => ep.Roles(AppRoles.Admin));
}

public sealed class GetServerSettingsEndpoint(AppDb db, IEmailSender email) : EndpointWithoutRequest<ServerSettingsRead>
{
    public override void Configure()
    {
        Get("");
        Group<AdminSettingsGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ServerSettingsRead.From(await ServerSettingsStore.GetAsync(db, ct), email.IsConfigured), ct);
}

public sealed class UpdateServerSettingsRequest
{
    public bool SignupEnabled { get; init; }
    public bool SignupRequireEmailVerification { get; init; }
    public bool SignupRequireAdminApproval { get; init; }
    public bool PublicShareableLinks { get; init; }
    public bool PublicShareableLinksUserInfo { get; init; }
    public bool SsoAutoRedirect { get; init; }
    public bool LocalLoginEnabled { get; init; } = true;
}

public sealed class UpdateServerSettingsEndpoint(AppDb db, IEmailSender email) : Endpoint<UpdateServerSettingsRequest, ServerSettingsRead>
{
    public override void Configure()
    {
        Put("");
        Group<AdminSettingsGroup>();
    }

    public override async Task HandleAsync(UpdateServerSettingsRequest req, CancellationToken ct)
    {
        // Password sign-in can only be turned off while a provider is there to sign in with.
        if (!req.LocalLoginEnabled && !await Sso.SsoRules.LocalLoginMayBeOffAsync(db, ct))
            ThrowError(r => r.LocalLoginEnabled, "Set up and enable a sign-in provider before turning off password sign-in.", StatusCodes.Status409Conflict);
        var settings = await ServerSettingsStore.SaveAsync(db, s =>
        {
            s.SignupEnabled = req.SignupEnabled;
            s.SignupRequireEmailVerification = req.SignupRequireEmailVerification;
            s.SignupRequireAdminApproval = req.SignupRequireAdminApproval;
            s.PublicShareableLinks = req.PublicShareableLinks;
            s.PublicShareableLinksUserInfo = req.PublicShareableLinksUserInfo;
            s.SsoAutoRedirect = req.SsoAutoRedirect;
            s.LocalLoginEnabled = req.LocalLoginEnabled;
        }, ct);
        await Send.OkAsync(ServerSettingsRead.From(settings, email.IsConfigured), ct);
    }
}

/// What a signed-out visitor (and the SPA's link button) needs to know: whether to offer a sign-up form and whether public links work.
public sealed class GetPublicSettingsEndpoint(AppDb db) : EndpointWithoutRequest<PublicSettingsRead>
{
    public override void Configure()
    {
        Get("/server-settings/public");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await ServerSettingsStore.GetAsync(db, ct);
        var providers = await db.IdentityProviders.AsNoTracking().Where(p => p.Enabled).OrderBy(p => p.Name).Select(p => new Sso.SsoProviderPublic(p.Slug, p.Name)).ToListAsync(ct);
        await Send.OkAsync(new PublicSettingsRead(settings.SignupEnabled, settings.PublicShareableLinks, settings.LocalLoginEnabled, settings.SsoAutoRedirect, providers), ct);
    }
}
