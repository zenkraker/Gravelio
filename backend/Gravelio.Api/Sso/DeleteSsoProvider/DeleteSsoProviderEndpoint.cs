using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sso;

/// Removes the provider and the links people made through it; they can still sign in with their password.
public sealed class DeleteSsoProviderEndpoint(AppDb db, SsoProviders sso) : Endpoint<IdRequest>
{
    public override void Configure()
    {
        Delete("{id:int}");
        Group<SsoAdminGroup>();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var provider = await db.IdentityProviders.FirstOrDefaultAsync(p => p.Id == req.Id, ct);
        if (provider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (provider.Enabled && !(await ServerSettingsStore.GetAsync(db, ct)).LocalLoginEnabled
            && !await db.IdentityProviders.AnyAsync(p => p.Enabled && p.Id != provider.Id, ct))
            ThrowError("Turn password sign-in back on before removing the last provider.", StatusCodes.Status409Conflict);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.UserLogins.Where(l => l.LoginProvider == provider.Slug).ExecuteDeleteAsync(ct);
        db.IdentityProviders.Remove(provider);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await sso.RefreshAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
