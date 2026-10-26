using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Sso;

public sealed class UnlinkSsoValidator : Validator<UnlinkSsoRequest>
{
    public UnlinkSsoValidator() => RuleFor(x => x.Slug).Matches(SsoRules.SlugPattern());
}
