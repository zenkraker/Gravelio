using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class StartSsoLinkValidator : Validator<StartSsoLinkRequest>
{
    public StartSsoLinkValidator() => RuleFor(x => x.Slug).Matches(SsoRules.SlugPattern());
}
