using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

/// The ranges Endurain enforces, shared by the create and update validators.
public abstract class WeightFieldsValidator<T> : Validator<T> where T : IWeightFields
{
    protected WeightFieldsValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the date");
        RuleFor(x => x.Weight).InclusiveBetween(0.01m, 500m); // kg
        RuleFor(x => x.Bmi).InclusiveBetween(0, 100m);
        RuleFor(x => x.BodyFat).InclusiveBetween(0, 100m);
        RuleFor(x => x.BodyWater).InclusiveBetween(0, 100m);
        RuleFor(x => x.BoneMass).InclusiveBetween(0, 500m);
        RuleFor(x => x.MuscleMass).InclusiveBetween(0, 500m);
        RuleFor(x => x.PhysiqueRating).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VisceralFat).InclusiveBetween(0, 100m);
        RuleFor(x => x.MetabolicAge).InclusiveBetween(0, 120);
    }
}
