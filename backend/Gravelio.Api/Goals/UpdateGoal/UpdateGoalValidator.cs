using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Goals;

public sealed class UpdateGoalValidator : Validator<UpdateGoalRequest>
{
    public UpdateGoalValidator()
    {
        RuleFor(x => x.Interval).Must(GoalRules.Intervals.Contains).WithMessage("Unknown interval");
        RuleFor(x => x.ActivityGroup).Must(GoalRules.Groups.ContainsKey).WithMessage("Unknown activity group");
        RuleFor(x => x.Metric).Must(GoalRules.Metrics.Contains).WithMessage("Unknown metric");
        RuleFor(x => x.Target).InclusiveBetween(1, GoalRules.MaxTarget);
    }
}
