using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class RejectFollowRequestEndpoint(AppDb db) : Endpoint<RejectFollowRequest>
{
    public override void Configure() => Delete("/follow-requests/{followerId:int}");

    public override async Task HandleAsync(RejectFollowRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        // Scoped to pending, so this cannot silently remove an accepted follower.
        var deleted = await db.Follows
            .Where(f => f.FollowerId == req.FollowerId && f.FolloweeId == me && f.Status == FollowStatuses.Pending)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
