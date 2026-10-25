using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Followers;

/// Used by both follower lists; <see cref="UserId"/> comes from the route.
public sealed class FollowListRequest : PagedRequest
{
    public int UserId { get; init; }
}

public sealed class FollowListValidator : PagedValidator<FollowListRequest>;
