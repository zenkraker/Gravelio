using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed class CreatePoopValidator : Validator<CreatePoopRequest>
{
    public CreatePoopValidator()
    {
        RuleFor(x => x.DateTime).NotEqual(default(DateTime)).WithMessage("Enter the date and time");
        RuleFor(x => x.BristolType).InclusiveBetween(1, 7);
        RuleFor(x => x.Color).Must(c => c is null || PoopColors.All.Contains(c)).WithMessage("Unknown color");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
