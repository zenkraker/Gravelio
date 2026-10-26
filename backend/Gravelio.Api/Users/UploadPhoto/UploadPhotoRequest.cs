namespace Gravelio.Api.Users;

public sealed class UploadPhotoRequest
{
    public required IFormFile File { get; init; }
}
