using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Notifications;

public sealed class ListNotificationsRequest : PagedRequest;

public sealed class ListNotificationsValidator : PagedValidator<ListNotificationsRequest>;

public sealed class ListNotificationsEndpoint(AppDb db) : Endpoint<ListNotificationsRequest, PagedResult<NotificationRead>>
{
    public override void Configure() => Get("/notifications");

    public override async Task HandleAsync(ListNotificationsRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        await Send.OkAsync(await db.Notifications.AsNoTracking().Where(n => n.UserId == me)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .ToPageAsync(req, page => page.Select(NotificationRead.Projection), ct), ct);
    }
}
