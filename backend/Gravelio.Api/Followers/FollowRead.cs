using Gravelio.Api.Users;

namespace Gravelio.Api.Followers;

public sealed record FollowRead(int FollowerId, int FolloweeId, string Status, PersonRead Follower, PersonRead Followee);

/// The caller's relationships with one other user, in both directions.
public sealed record RelationshipRead(FollowRead? Outgoing, FollowRead? Incoming);

/// A profile as seen by another signed-in user, with the caller's relationship to it.
public sealed record PersonProfileRead(PersonRead Person, int Followers, int Following, FollowRead? Outgoing, FollowRead? Incoming);
