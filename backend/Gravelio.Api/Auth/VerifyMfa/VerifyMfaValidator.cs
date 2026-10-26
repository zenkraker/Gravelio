using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Auth;

public sealed class VerifyMfaValidator : Validator<VerifyMfaRequest>
{
    public VerifyMfaValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
}
