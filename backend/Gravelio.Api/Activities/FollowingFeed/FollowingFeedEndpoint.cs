using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Followers;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class FollowingFeedRequest : PagedRequest;

public sealed class FollowingFeedValidator : PagedValidator<FollowingFeedRequest>;

/// Activity from the users the caller has accepted follows for; private activities never enter the feed.
public sealed class FollowingFeedEndpoint(AppDb db) : Endpoint<FollowingFeedRequest, PagedResult<ActivityRead>>
{
    public override void Configure() => Get("/activities/feed");

    public override async Task HandleAsync(FollowingFeedRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var followeeIds = db.Follows.AcceptedFolloweeIds(me).Where(id => db.Users.Any(u => u.Id == id && u.Active));
        var page = await db.Activities.AsNoTracking()
            .Where(a => followeeIds.Contains(a.UserId) && !a.IsHidden
                && (a.Visibility == Visibilities.Public || a.Visibility == Visibilities.Followers))
            .OrderByDescending(a => a.StartTime).ThenByDescending(a => a.Id)
            .ToPageAsync(req, page => page.Select(ActivityRead.Projection), ct);
        await Send.OkAsync(await PrivacyMask.ApplyAsync(db, page, me, ct), ct);
    }
}
