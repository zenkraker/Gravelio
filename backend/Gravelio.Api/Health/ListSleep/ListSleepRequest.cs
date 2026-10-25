using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class ListSleepRequest : PagedRequest;

public sealed class ListSleepValidator : PagedValidator<ListSleepRequest>;
