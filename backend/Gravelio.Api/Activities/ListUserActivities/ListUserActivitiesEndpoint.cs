using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ListUserActivitiesRequest : PagedRequest
{
    /// The activity owner, from the route.
    public int UserId { get; init; }
}

public sealed class ListUserActivitiesValidator : PagedValidator<ListUserActivitiesRequest>;

public sealed class ListUserActivitiesEndpoint(AppDb db) : Endpoint<ListUserActivitiesRequest, PagedResult<ActivityRead>>
{
    public override void Configure() => Get("/users/{userId:int}/activities");

    public override async Task HandleAsync(ListUserActivitiesRequest req, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == req.UserId && u.Active, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var me = User.UserId();
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == req.UserId);
        if (req.UserId != me) query = query.VisibleTo(db, me);
        var page = await query.OrderByDescending(a => a.StartTime).ThenByDescending(a => a.Id)
            .ToPageAsync(req, page => page.Select(ActivityRead.Projection), ct);
        if (req.UserId != me) page = await PrivacyMask.ApplyAsync(db, page, me, ct);
        await Send.OkAsync(page, ct);
    }
}
