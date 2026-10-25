using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListGearsValidator : PagedValidator<ListGearsRequest>
{
    public ListGearsValidator()
    {
        RuleFor(x => x.Q).MaximumLength(250);
        RuleFor(x => x.GearType!.Value).Must(GearTypes.IsValid).When(x => x.GearType is not null).WithMessage("Unknown gear type");
    }
}
