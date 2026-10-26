using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.DataTransfer;

public sealed class ImportDataValidator : Validator<ImportDataRequest>
{
    public ImportDataValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the export zip in the 'file' form field.");
        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length).InclusiveBetween(1, ImportDataEndpoint.MaxUploadBytes).WithMessage("File is empty or larger than 500 MB");
            RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Equal(".zip").WithMessage("Upload the .zip file the export gave you");
        });
    }
}
