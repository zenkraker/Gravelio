using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Privacy;

namespace Gravelio.Api.Activities;

public sealed class UpdateActivityPrivateDetailsValidator : Validator<UpdateActivityPrivateDetailsRequest>
{
    public UpdateActivityPrivateDetailsValidator()
    {
        RuleFor(x => x.PrivateNotes).MaximumLength(2500);
        RuleFor(x => x.WorkoutFeeling).InclusiveBetween(0, 100);
        RuleFor(x => x.WorkoutRpe).InclusiveBetween(10, 100);
        RuleForEach(x => x.HiddenDetails).Must(HiddenDetailNames.ByName.ContainsKey).WithMessage("Unknown detail '{PropertyValue}'");
    }
}
