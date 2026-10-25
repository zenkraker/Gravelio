using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Followers;

public sealed class PeopleListRequest : PagedRequest
{
    /// Matched against name and username, ignoring case.
    public string? Q { get; init; }
}

public sealed class PeopleListValidator : PagedValidator<PeopleListRequest>
{
    public PeopleListValidator() => RuleFor(x => x.Q).MaximumLength(250);
}
