using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class UpdateActivityValidator : Validator<UpdateActivityRequest>
{
    public UpdateActivityValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2500);
        RuleFor(x => x.ActivityType).Must(ActivityTypes.Names.ContainsKey).WithMessage("Unknown activity type");
        RuleFor(x => x.Visibility).InclusiveBetween(Visibilities.Public, Visibilities.Private);
    }
}
