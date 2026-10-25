using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Followers;

public sealed class ListFollowRequestsRequest : PagedRequest;

public sealed class ListFollowRequestsValidator : PagedValidator<ListFollowRequestsRequest>;
