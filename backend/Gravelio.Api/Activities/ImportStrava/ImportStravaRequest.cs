using FastEndpoints;

namespace Gravelio.Api.Activities;

public sealed class ImportStravaRequest
{
    public required IFormFile File { get; init; }
}
