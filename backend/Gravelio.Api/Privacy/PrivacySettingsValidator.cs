using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;

namespace Gravelio.Api.Privacy;

public sealed class PrivacySettingsValidator : Validator<PrivacySettingsRequest>
{
    public PrivacySettingsValidator() =>
        RuleFor(x => x.DefaultActivityVisibility).InclusiveBetween(Visibilities.Public, Visibilities.Private);
}
