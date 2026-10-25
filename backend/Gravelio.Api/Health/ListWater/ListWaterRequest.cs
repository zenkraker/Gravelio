using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class ListWaterRequest : PagedRequest;

public sealed class ListWaterValidator : PagedValidator<ListWaterRequest>;
