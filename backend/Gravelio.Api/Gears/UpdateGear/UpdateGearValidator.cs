using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class UpdateGearValidator : Validator<UpdateGearRequest>
{
    public UpdateGearValidator()
    {
        RuleFor(x => x.Nickname).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Brand).MaximumLength(250);
        RuleFor(x => x.Model).MaximumLength(250);
        RuleFor(x => x.GearType).Must(GearTypes.IsValid).WithMessage("Unknown gear type");
        RuleFor(x => x.InitialDistance).InclusiveBetween(0, 10_000_000); // meters
        RuleFor(x => x.PurchaseValue).InclusiveBetween(0, 1_000_000_000m);
    }
}
