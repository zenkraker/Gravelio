namespace Gravelio.Api.Followers;

/// <see cref="UserId"/> is the followee and <see cref="FollowerId"/> the follower; both come from the route.
public sealed record DeleteFollowRequest(int UserId, int FollowerId);
