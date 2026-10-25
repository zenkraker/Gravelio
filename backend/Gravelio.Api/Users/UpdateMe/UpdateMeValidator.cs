using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed class UpdateMeValidator : Validator<UpdateMeRequest>
{
    public UpdateMeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(250);
        RuleFor(x => x.City).MaximumLength(250);
        RuleFor(x => x.PreferredLanguage).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Gender).Must(UserRules.Genders.Contains).WithMessage("Unknown gender");
        RuleFor(x => x.Units).Must(UserRules.Units.Contains).WithMessage("Unknown units");
        RuleFor(x => x.Height).InclusiveBetween(50, 300);
        RuleFor(x => x.MaxHeartRate).InclusiveBetween(60, 250);
        RuleFor(x => x.FirstDayOfWeek).Must(UserRules.Days.Contains).WithMessage("Unknown day of week");
        RuleFor(x => x.Currency).Must(UserRules.Currencies.Contains).WithMessage("Unknown currency");
        RuleFor(x => x.Timezone).Must(tz => TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _)).WithMessage("Unknown timezone");
    }
}
