using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public static class FollowQueries
{
    public static IQueryable<FollowRead> ToRead(this IQueryable<Follow> follows) =>
        follows.Select(f => new FollowRead(f.FollowerId, f.FolloweeId, f.Status,
            new PersonRead(f.Follower.Id, f.Follower.Name, f.Follower.UserName!, f.Follower.City, f.Follower.HasPhoto),
            new PersonRead(f.Followee.Id, f.Followee.Name, f.Followee.UserName!, f.Followee.City, f.Followee.HasPhoto)));

    public static IQueryable<int> AcceptedFolloweeIds(this IQueryable<Follow> follows, int followerId) =>
        follows.Where(f => f.FollowerId == followerId && f.Status == FollowStatuses.Accepted).Select(f => f.FolloweeId);

    /// A user's follower/following lists are visible to the user and to their accepted followers.
    public static async Task<bool> MayViewNetworkAsync(AppDb db, int targetUserId, int requesterUserId, CancellationToken ct) =>
        targetUserId == requesterUserId ||
        await db.Follows.AnyAsync(f => f.FollowerId == requesterUserId && f.FolloweeId == targetUserId && f.Status == FollowStatuses.Accepted, ct);
}
