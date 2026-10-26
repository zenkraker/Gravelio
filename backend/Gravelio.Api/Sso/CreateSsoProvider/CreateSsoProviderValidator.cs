using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class CreateSsoProviderValidator : Validator<CreateSsoProviderRequest>
{
    public CreateSsoProviderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).Matches(SsoRules.SlugPattern()).WithMessage("Use lowercase letters, digits and hyphens (at most 40).");
        RuleFor(x => x.Issuer).ValidUrl().MaximumLength(500);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientSecret).MaximumLength(500);
        RuleFor(x => x.Scopes).MaximumLength(200).ValidScopes();
    }
}
