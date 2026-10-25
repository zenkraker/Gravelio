using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class AcceptFollowRequestEndpoint(AppDb db) : Endpoint<AcceptFollowRequest, FollowRead>
{
    public override void Configure() => Post("/follow-requests/{followerId:int}/accept");

    public override async Task HandleAsync(AcceptFollowRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        // One conditional update: two concurrent accepts cannot both report success.
        var updated = await db.Follows
            .Where(f => f.FollowerId == req.FollowerId && f.FolloweeId == me && f.Status == FollowStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FollowStatuses.Accepted), ct);
        if (updated == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var read = await db.Follows.AsNoTracking().Where(f => f.FollowerId == req.FollowerId && f.FolloweeId == me)
            .ToRead().FirstAsync(ct);
        await Send.OkAsync(read, ct);
    }
}
