using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

/// Unfollowing and removing a follower are the same row; the caller must be one of the two parties.
public sealed class DeleteFollowEndpoint(AppDb db) : Endpoint<DeleteFollowRequest>
{
    public override void Configure() => Delete("/users/{userId:int}/followers/{followerId:int}");

    public override async Task HandleAsync(DeleteFollowRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        if (me != req.UserId && me != req.FollowerId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var deleted = await db.Follows.Where(f => f.FollowerId == req.FollowerId && f.FolloweeId == req.UserId).ExecuteDeleteAsync(ct);
        if (deleted > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
