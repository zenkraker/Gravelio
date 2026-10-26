using Gravelio.Api.Data;

namespace Gravelio.Api.Activities;

public static class ActivityQueries
{
    /// What a public link may open: a public activity of an active user. The server setting is checked by the endpoint.
    public static IQueryable<Activity> PubliclyLinkable(this IQueryable<Activity> activities) =>
        activities.Where(a => a.Visibility == Visibilities.Public && a.User.Active);

    /// Non-owners see public activities, plus followers-only activities when they are an accepted follower.
    /// A deactivated owner's activities disappear from other users; the owner can still read them.
    public static IQueryable<Activity> VisibleTo(this IQueryable<Activity> activities, AppDb db, int requesterUserId) =>
        activities.Where(a => a.UserId == requesterUserId
            || (a.User.Active
                && (a.Visibility == Visibilities.Public
                    || (a.Visibility == Visibilities.Followers
                        && db.Follows.Any(f => f.FollowerId == requesterUserId && f.FolloweeId == a.UserId && f.Status == FollowStatuses.Accepted)))));
}
