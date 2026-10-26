using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Mfa;

public sealed class RegenerateRecoveryCodesValidator : Validator<RegenerateRecoveryCodesRequest>
{
    public RegenerateRecoveryCodesValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}
