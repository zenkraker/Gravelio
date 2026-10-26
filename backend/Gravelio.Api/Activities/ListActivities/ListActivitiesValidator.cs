using FluentValidation;

namespace Gravelio.Api.Activities;

public sealed class ListActivitiesValidator : PagedValidator<ListActivitiesRequest>
{
    public ListActivitiesValidator()
    {
        RuleFor(x => x.Type).Must(t => ActivityTypes.Names.ContainsKey(t!.Value)).When(x => x.Type is not null).WithMessage("Unknown activity type");
        RuleFor(x => x.Q).MaximumLength(250);
        RuleFor(x => x.Sort).Must(ActivityQueries.SortKeys.Contains).WithMessage("Unknown sort key");
        RuleFor(x => x.Order).Must(o => o is "asc" or "desc").WithMessage("Order must be asc or desc");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("The end date is before the start date");
    }
}
