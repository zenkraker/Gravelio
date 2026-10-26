using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class StartSsoLoginValidator : Validator<StartSsoLoginRequest>
{
    public StartSsoLoginValidator() => RuleFor(x => x.Slug).Matches(SsoRules.SlugPattern());
}
