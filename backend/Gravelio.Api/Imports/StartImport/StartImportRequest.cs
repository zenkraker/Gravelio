using FastEndpoints;

namespace Gravelio.Api.Imports;

public sealed class StartImportRequest
{
    public required IFormFile File { get; init; }

    /// Optional: sending the same zip again with the same key returns the job it started instead of starting a second one.
    [FromHeader("Idempotency-Key", IsRequired = false)]
    public string? IdempotencyKey { get; init; }
}
