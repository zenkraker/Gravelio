namespace Gravelio.Api.Followers;

/// <see cref="FollowerId"/> is the user whose pending request is accepted, from the route.
public sealed record AcceptFollowRequest(int FollowerId);
