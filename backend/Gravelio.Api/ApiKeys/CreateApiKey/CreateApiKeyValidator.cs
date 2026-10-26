using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.ApiKeys;

public sealed class CreateApiKeyValidator : Validator<CreateApiKeyRequest>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 365);
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).MaximumLength(32);
    }
}
