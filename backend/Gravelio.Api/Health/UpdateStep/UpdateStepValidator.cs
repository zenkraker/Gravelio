using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Health;

public sealed class UpdateStepValidator : Validator<UpdateStepRequest>
{
    public UpdateStepValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Enter the date");
        RuleFor(x => x.Steps).GreaterThanOrEqualTo(0);
    }
}
