namespace Gravelio.Api.DataTransfer;

public sealed class ImportDataRequest
{
    public required IFormFile File { get; init; }
}
