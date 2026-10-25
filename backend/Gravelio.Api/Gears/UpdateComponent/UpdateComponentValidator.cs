using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class UpdateComponentValidator : Validator<UpdateComponentRequest>
{
    public UpdateComponentValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brand).MaximumLength(250);
        RuleFor(x => x.Model).MaximumLength(250);
        RuleFor(x => x.PurchaseDate).NotEqual(default(DateOnly)).WithMessage("Enter the purchase date");
        RuleFor(x => x.RetiredDate!.Value).GreaterThanOrEqualTo(x => x.PurchaseDate).When(x => x.RetiredDate is not null)
            .WithMessage("A component can't be retired before it was bought");
        RuleFor(x => x.ExpectedDistance).InclusiveBetween(0, 10_000_000); // meters
        RuleFor(x => x.PurchaseValue).InclusiveBetween(0, 1_000_000_000m);
    }
}
