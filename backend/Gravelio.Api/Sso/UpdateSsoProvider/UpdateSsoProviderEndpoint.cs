using FastEndpoints;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Sso;

/// The slug never changes: linked accounts are stored under it.
public sealed class UpdateSsoProviderEndpoint(AppDb db, SsoProviders sso, IDataProtectionProvider protection, IOptions<OutboundOptions> outbound)
    : Endpoint<UpdateSsoProviderRequest, SsoProviderRead>
{
    public override void Configure()
    {
        Put("{id:int}");
        Group<SsoAdminGroup>();
    }

    public override async Task HandleAsync(UpdateSsoProviderRequest req, CancellationToken ct)
    {
        var provider = await db.IdentityProviders.FirstOrDefaultAsync(p => p.Id == req.Id, ct);
        if (provider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!SsoGuard.IsSafeIssuer(req.Issuer, outbound.Value)) ThrowError(r => r.Issuer, SsoGuard.HttpsRequired);
        // Turning off the last provider while password sign-in is off would leave nobody able to sign in.
        if (!req.Enabled && provider.Enabled && !(await ServerSettingsStore.GetAsync(db, ct)).LocalLoginEnabled
            && !await db.IdentityProviders.AnyAsync(p => p.Enabled && p.Id != provider.Id, ct))
            ThrowError("Turn password sign-in back on before disabling the last provider.", StatusCodes.Status409Conflict);

        provider.Name = req.Name.Trim();
        provider.Issuer = req.Issuer.Trim();
        provider.ClientId = req.ClientId.Trim();
        if (req.ClearClientSecret) provider.ClientSecret = null;
        else if (!string.IsNullOrEmpty(req.ClientSecret)) provider.ClientSecret = SsoProviders.Protect(protection, req.ClientSecret);
        provider.Scopes = string.IsNullOrWhiteSpace(req.Scopes) ? SsoRules.DefaultScopes : req.Scopes.Trim();
        provider.Enabled = req.Enabled;
        provider.AutoCreateUsers = req.AutoCreateUsers;
        await db.SaveChangesAsync(ct);
        await sso.RefreshAsync(ct);
        await Send.OkAsync(new SsoProviderRead(provider.Id, provider.Name, provider.Slug, provider.Issuer, provider.ClientId, provider.ClientSecret is not null,
            provider.Scopes, provider.Enabled, provider.AutoCreateUsers), ct);
    }
}
