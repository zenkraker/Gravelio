using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed class CompleteFastingValidator : Validator<CompleteFastingRequest>
{
    public CompleteFastingValidator()
    {
        RuleFor(x => x.FastEndTime).NotEqual(default(DateTime)).WithMessage("Enter the end time");
        RuleFor(x => x.Status).Must(s => s is FastingStatuses.Completed or FastingStatuses.Broken or FastingStatuses.Cancelled)
            .WithMessage("A fast can only be completed, broken or cancelled");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
