using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class CreateWaterValidator : Validator<CreateWaterRequest>
{
    public CreateWaterValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the date");
        RuleFor(x => x.AmountMl).InclusiveBetween(0, 20_000);
    }
}
