using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class UpdateSsoProviderValidator : Validator<UpdateSsoProviderRequest>
{
    public UpdateSsoProviderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Issuer).ValidUrl().MaximumLength(500);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientSecret).MaximumLength(500);
        RuleFor(x => x.Scopes).MaximumLength(200).ValidScopes();
    }
}
