using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Activities;

public sealed class ImportStravaValidator : Validator<ImportStravaRequest>
{
    public ImportStravaValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the Strava export zip in the 'file' form field.");
        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length).InclusiveBetween(1, ImportStravaEndpoint.MaxUploadBytes).WithMessage("File is empty or larger than 500 MB");
            RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Equal(".zip").WithMessage("Upload the .zip file Strava sends you");
        });
    }
}
