using Gravelio.Api.Data;

namespace Gravelio.Api.Activities;

public static class ActivityQueries
{
    public static readonly string[] SortKeys = ["start_time", "name", "type", "distance", "duration", "elevation", "calories", "average_hr"];

    /// Orders by one of <see cref="SortKeys"/>; the id breaks ties so paging never skips or repeats a row.
    public static IOrderedQueryable<Activity> Sorted(this IQueryable<Activity> activities, string key, bool descending) => (key, descending) switch
    {
        ("name", false) => activities.OrderBy(a => a.Name).ThenBy(a => a.Id),
        ("name", true) => activities.OrderByDescending(a => a.Name).ThenByDescending(a => a.Id),
        ("type", false) => activities.OrderBy(a => a.ActivityType).ThenBy(a => a.Id),
        ("type", true) => activities.OrderByDescending(a => a.ActivityType).ThenByDescending(a => a.Id),
        ("distance", false) => activities.OrderBy(a => a.Distance).ThenBy(a => a.Id),
        ("distance", true) => activities.OrderByDescending(a => a.Distance).ThenByDescending(a => a.Id),
        ("duration", false) => activities.OrderBy(a => a.TotalTimerTime).ThenBy(a => a.Id),
        ("duration", true) => activities.OrderByDescending(a => a.TotalTimerTime).ThenByDescending(a => a.Id),
        ("elevation", false) => activities.OrderBy(a => a.ElevationGain).ThenBy(a => a.Id),
        ("elevation", true) => activities.OrderByDescending(a => a.ElevationGain).ThenByDescending(a => a.Id),
        ("calories", false) => activities.OrderBy(a => a.Calories).ThenBy(a => a.Id),
        ("calories", true) => activities.OrderByDescending(a => a.Calories).ThenByDescending(a => a.Id),
        ("average_hr", false) => activities.OrderBy(a => a.AverageHr).ThenBy(a => a.Id),
        ("average_hr", true) => activities.OrderByDescending(a => a.AverageHr).ThenByDescending(a => a.Id),
        (_, false) => activities.OrderBy(a => a.StartTime).ThenBy(a => a.Id),
        _ => activities.OrderByDescending(a => a.StartTime).ThenByDescending(a => a.Id),
    };

    /// What a public link may open: a public activity of an active user. The server setting is checked by the endpoint.
    public static IQueryable<Activity> PubliclyLinkable(this IQueryable<Activity> activities) =>
        activities.Where(a => a.Visibility == Visibilities.Public && !a.IsHidden && a.User.Active);

    /// Non-owners see public activities, plus followers-only activities when they are an accepted follower.
    /// A deactivated owner's activities disappear from other users; the owner can still read them.
    public static IQueryable<Activity> VisibleTo(this IQueryable<Activity> activities, AppDb db, int requesterUserId) =>
        activities.Where(a => a.UserId == requesterUserId
            || (a.User.Active && !a.IsHidden
                && (a.Visibility == Visibilities.Public
                    || (a.Visibility == Visibilities.Followers
                        && db.Follows.Any(f => f.FollowerId == requesterUserId && f.FolloweeId == a.UserId && f.Status == FollowStatuses.Accepted)))));
}
