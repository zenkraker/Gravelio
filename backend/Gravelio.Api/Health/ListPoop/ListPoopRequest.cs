using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class ListPoopRequest : PagedRequest;

public sealed class ListPoopValidator : PagedValidator<ListPoopRequest>;
