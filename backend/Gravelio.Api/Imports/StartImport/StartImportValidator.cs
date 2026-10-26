using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Imports;

public sealed class StartImportValidator : Validator<StartImportRequest>
{
    public StartImportValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the zip in the 'file' form field.");
        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length).InclusiveBetween(1, StartImportEndpoint.MaxUploadBytes).WithMessage("The zip is empty or larger than 500 MB.");
            RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Equal(".zip").WithMessage("Upload a .zip file.");
        });
        RuleFor(x => x.IdempotencyKey).MaximumLength(100);
    }
}
