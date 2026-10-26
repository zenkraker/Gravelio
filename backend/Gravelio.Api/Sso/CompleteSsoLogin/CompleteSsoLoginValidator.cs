using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class CompleteSsoLoginValidator : Validator<CompleteSsoLoginRequest>
{
    public CompleteSsoLoginValidator() => RuleFor(x => x.Slug).Matches(SsoRules.SlugPattern());
}
