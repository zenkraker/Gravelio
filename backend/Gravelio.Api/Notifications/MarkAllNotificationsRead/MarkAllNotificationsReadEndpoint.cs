using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Notifications;

public sealed class MarkAllNotificationsReadEndpoint(AppDb db) : EndpointWithoutRequest
{
    public override void Configure() => Put("/notifications/read-all");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var me = User.UserId();
        await db.Notifications.Where(n => n.UserId == me && !n.Read)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true), ct);
        await Send.NoContentAsync(ct);
    }
}
