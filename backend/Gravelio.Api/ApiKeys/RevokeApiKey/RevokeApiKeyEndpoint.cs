using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.ApiKeys;

/// Stops a key from working but keeps its row, so the list still shows when it was made and last used.
public sealed class RevokeApiKeyEndpoint(AppDb db, TimeProvider time) : Endpoint<IdRequest>
{
    public override void Configure() => Post("/users/me/api-keys/{id:int}/revoke");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var found = await db.ApiKeys.OwnedBy(User.UserId(), req.Id).AnyAsync(ct);
        if (!found)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await db.ApiKeys.OwnedBy(User.UserId(), req.Id).Where(k => k.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, now), ct);
        await Send.NoContentAsync(ct);
    }
}
