using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class UploadActivityValidator : Validator<UploadActivityRequest>
{
    public UploadActivityValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the activity file in the 'file' form field.");
        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length).InclusiveBetween(1, UploadActivityEndpoint.MaxUploadBytes).WithMessage("File is empty or larger than 50 MB");
            RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Must(ActivityFileParser.SupportedExtensions.Contains)
                .WithMessage($"Unsupported file type. Allowed: {string.Join(", ", ActivityFileParser.SupportedExtensions)}");
        });
    }
}
