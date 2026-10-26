using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

/// Creates a pending request; the follow takes effect only when the target accepts it.
public sealed class FollowUserEndpoint(AppDb db, TimeProvider time, ILogger<FollowUserEndpoint> log) : Endpoint<FollowUserRequest, FollowRead>
{
    public override void Configure() => Post("/users/{userId:int}/followers");

    public override async Task HandleAsync(FollowUserRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        if (req.UserId == me) ThrowError("You cannot follow yourself", StatusCodes.Status400BadRequest);
        if (!await db.Users.AnyAsync(u => u.Id == req.UserId && u.Active, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (await db.Follows.AnyAsync(f => f.FollowerId == me && f.FolloweeId == req.UserId, ct))
            ThrowError("You already follow this user or have a pending request.", StatusCodes.Status409Conflict);

        var follow = new Follow { FollowerId = me, FolloweeId = req.UserId, Status = FollowStatuses.Pending, CreatedAt = time.GetUtcNow().UtcDateTime };
        db.Follows.Add(follow);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("You already follow this user or have a pending request.", StatusCodes.Status409Conflict);
        var read = await db.Follows.AsNoTracking().Where(f => f.Id == follow.Id).ToRead().FirstAsync(ct);
        NotificationWriter.Add(db, req.UserId, NotificationTypes.NewFollowerRequest, me, read.Follower.Name, follow.CreatedAt);
        await NotificationWriter.TrySaveAsync(db, log, ct);
        await Send.CreatedAtAsync<GetRelationshipEndpoint>(new { id = req.UserId }, read, cancellation: ct);
    }
}
