using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed class CreateFastingValidator : Validator<CreateFastingRequest>
{
    public CreateFastingValidator()
    {
        RuleFor(x => x.FastStartTime).NotEqual(default(DateTime)).WithMessage("Enter the start time");
        RuleFor(x => x.TargetDurationSeconds).InclusiveBetween(0, 259_200);
        RuleFor(x => x.FastingType).Must(t => t is null || FastingTypes.All.Contains(t)).WithMessage("Unknown fasting type");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x).Must(x => x.FastEndTime is null || x.FastEndTime > x.FastStartTime)
            .WithMessage("The fast cannot end before it starts");
    }
}
