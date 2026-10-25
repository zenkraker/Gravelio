using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class ListFollowingEndpoint(AppDb db) : Endpoint<FollowListRequest, PagedResult<FollowRead>>
{
    public override void Configure() => Get("/users/{userId:int}/following");

    public override async Task HandleAsync(FollowListRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        if (!await db.Users.AnyAsync(u => u.Id == req.UserId && u.Active, ct) || !await FollowQueries.MayViewNetworkAsync(db, req.UserId, me, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(await db.Follows.AsNoTracking()
            .Where(f => f.FollowerId == req.UserId && f.Status == FollowStatuses.Accepted)
            .OrderBy(f => f.Id)
            .ToPageAsync(req, page => page.ToRead(), ct), ct);
    }
}
