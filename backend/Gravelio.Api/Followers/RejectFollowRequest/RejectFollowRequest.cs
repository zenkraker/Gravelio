namespace Gravelio.Api.Followers;

/// <see cref="FollowerId"/> is the user whose pending request is declined, from the route.
public sealed record RejectFollowRequest(int FollowerId);
