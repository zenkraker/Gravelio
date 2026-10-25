using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class SaveTargetsValidator : Validator<SaveTargetsRequest>
{
    public SaveTargetsValidator()
    {
        RuleFor(x => x.Weight).InclusiveBetween(0.01m, 500m);
        RuleFor(x => x.Steps).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Sleep).InclusiveBetween(0, 86_400);
        RuleFor(x => x.Fasting).InclusiveBetween(0, 259_200);
        RuleFor(x => x.WaterMl).InclusiveBetween(0, 20_000);
        RuleFor(x => x.PoopCount).InclusiveBetween(0, 20);
    }
}
