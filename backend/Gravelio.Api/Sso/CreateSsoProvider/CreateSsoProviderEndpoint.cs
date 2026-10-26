using FastEndpoints;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Sso;

public sealed class CreateSsoProviderEndpoint(AppDb db, SsoProviders sso, IDataProtectionProvider protection, IOptions<OutboundOptions> outbound)
    : Endpoint<CreateSsoProviderRequest, SsoProviderRead>
{
    public override void Configure()
    {
        Post("");
        Group<SsoAdminGroup>();
    }

    public override async Task HandleAsync(CreateSsoProviderRequest req, CancellationToken ct)
    {
        if (!SsoGuard.IsSafeIssuer(req.Issuer, outbound.Value)) ThrowError(r => r.Issuer, SsoGuard.HttpsRequired);
        var provider = new IdentityProvider
        {
            Name = req.Name.Trim(),
            Slug = req.Slug,
            Issuer = req.Issuer.Trim(),
            ClientId = req.ClientId.Trim(),
            ClientSecret = string.IsNullOrEmpty(req.ClientSecret) ? null : SsoProviders.Protect(protection, req.ClientSecret),
            Scopes = string.IsNullOrWhiteSpace(req.Scopes) ? SsoRules.DefaultScopes : req.Scopes.Trim(),
            Enabled = req.Enabled,
            AutoCreateUsers = req.AutoCreateUsers,
        };
        db.IdentityProviders.Add(provider);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError(r => r.Slug, "A provider with this address name already exists.", StatusCodes.Status409Conflict);
        await sso.RefreshAsync(ct);
        await Send.ResponseAsync(new SsoProviderRead(provider.Id, provider.Name, provider.Slug, provider.Issuer, provider.ClientId, provider.ClientSecret is not null,
            provider.Scopes, provider.Enabled, provider.AutoCreateUsers), StatusCodes.Status201Created, ct);
    }
}
