using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Notifications;

public sealed class MarkNotificationReadEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Put("/notifications/{id:int}/read");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        // Another user's notification is indistinguishable from a missing one.
        var updated = await db.Notifications.Where(n => n.Id == req.Id && n.UserId == User.UserId())
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true), ct);
        if (updated == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}
