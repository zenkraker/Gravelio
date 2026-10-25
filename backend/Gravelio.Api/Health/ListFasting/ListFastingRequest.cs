using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class ListFastingRequest : PagedRequest;

public sealed class ListFastingValidator : PagedValidator<ListFastingRequest>;
