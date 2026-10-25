namespace Gravelio.Api.Followers;

/// <see cref="UserId"/> is the user to follow, from the route.
public sealed record FollowUserRequest(int UserId);
