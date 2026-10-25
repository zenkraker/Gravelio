using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
