using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Email;

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
        var settings = await ServerSettingsStore.SaveAsync(db, s =>
        {
            s.SignupEnabled = req.SignupEnabled;
            s.SignupRequireEmailVerification = req.SignupRequireEmailVerification;
            s.SignupRequireAdminApproval = req.SignupRequireAdminApproval;
        }, ct);
        await Send.OkAsync(ServerSettingsRead.From(settings, email.IsConfigured), ct);
    }
}

/// The one setting a signed-out visitor needs: whether to offer a sign-up form.
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
        await Send.OkAsync(new PublicSettingsRead(settings.SignupEnabled), ct);
    }
}
