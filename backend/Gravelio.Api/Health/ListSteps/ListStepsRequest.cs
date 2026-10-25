using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class ListStepsRequest : PagedRequest;

public sealed class ListStepsValidator : PagedValidator<ListStepsRequest>;
