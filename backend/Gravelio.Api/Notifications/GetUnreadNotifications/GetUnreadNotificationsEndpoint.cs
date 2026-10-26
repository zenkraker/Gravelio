using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Notifications;

public sealed record UnreadNotificationsRead(int Count);

public sealed class GetUnreadNotificationsEndpoint(AppDb db) : EndpointWithoutRequest<UnreadNotificationsRead>
{
    public override void Configure() => Get("/notifications/unread-count");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var me = User.UserId();
        await Send.OkAsync(new UnreadNotificationsRead(await db.Notifications.CountAsync(n => n.UserId == me && !n.Read, ct)), ct);
    }
}
