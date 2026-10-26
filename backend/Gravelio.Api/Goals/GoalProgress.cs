using Gravelio.Api.Activities;
using Gravelio.Api.Data;

namespace Gravelio.Api.Goals;

/// How far a goal has got in the period that contains the anchor date. <see cref="End"/> is the last day of the period.
public sealed record GoalProgressRead(int GoalId, string Interval, string ActivityGroup, string Metric, int Target, double Total, int Percent,
    DateOnly Start, DateOnly End);

public static class GoalProgress
{
    /// The local date range [Start, End) a goal's period covers around <paramref name="anchor"/>.
    public static (DateOnly Start, DateOnly End) Window(string interval, DateOnly anchor, DayOfWeek firstDay) => interval switch
    {
        "daily" => (anchor, anchor.AddDays(1)),
        "weekly" => ActivityStats.Range("week", anchor, firstDay, null),
        "monthly" => ActivityStats.Range("month", anchor, firstDay, null),
        _ => ActivityStats.Range("year", anchor, firstDay, null),
    };

    /// Pure, so the period maths can be tested without a database. An activity counts on the local day it was recorded on, not its UTC day.
    public static GoalProgressRead Compute(Goal goal, IReadOnlyList<ActivityStats.Row> rows, DateOnly anchor, DayOfWeek firstDay, string fallbackTimezone)
    {
        var (start, end) = Window(goal.Interval, anchor, firstDay);
        var types = GoalRules.Groups[goal.ActivityGroup];
        var inPeriod = rows.Where(r => types.Contains(r.ActivityType))
            .Where(r => ActivityStats.LocalDate(r, fallbackTimezone) is var day && day >= start && day < end).ToList();
        double total = goal.Metric switch
        {
            "calories" => inPeriod.Sum(r => (double)(r.Calories ?? 0)),
            "activities" => inPeriod.Count,
            "distance" => inPeriod.Sum(r => (double)r.Distance),
            "elevation" => inPeriod.Sum(r => (double)(r.ElevationGain ?? 0)),
            _ => inPeriod.Sum(r => r.TimerTime), // moving time, like the statistics page
        };
        var percent = (int)Math.Min(100, Math.Round(total * 100 / goal.Target));
        return new GoalProgressRead(goal.Id, goal.Interval, goal.ActivityGroup, goal.Metric, goal.Target, total, percent, start, end.AddDays(-1));
    }
}
