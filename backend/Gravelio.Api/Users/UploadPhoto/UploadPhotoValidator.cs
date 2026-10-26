using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Users;

public sealed class UploadPhotoValidator : Validator<UploadPhotoRequest>
{
    public UploadPhotoValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the picture in the 'file' form field.");
        When(x => x.File is not null, () =>
            RuleFor(x => x.File.Length).InclusiveBetween(1, JpegPhoto.MaxBytes).WithMessage("The picture must be a JPEG of at most 512 KB."));
    }
}
