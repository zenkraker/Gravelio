using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Username).Matches(UserRules.UsernamePattern).WithMessage("Username may only contain letters, digits and dots (max 45).");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(250);
        RuleFor(x => x.AccessType).Must(UserRules.AccessTypes.Contains).WithMessage("Unknown access type");
        RuleFor(x => x.Password).MaximumLength(128);
        RuleFor(x => x.Password).Must(p => p is null or { Length: >= AdminPasswordValidator.AdminMinLength })
            .When(x => x.AccessType == AppRoles.Admin).WithMessage(AdminPasswordValidator.TooShortMessage);
    }
}
