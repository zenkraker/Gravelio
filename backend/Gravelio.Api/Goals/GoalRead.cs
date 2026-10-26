using Gravelio.Api.Data;

namespace Gravelio.Api.Goals;

public sealed record GoalRead(int Id, string Interval, string ActivityGroup, string Metric, int Target)
{
    public static GoalRead From(Goal g) => new(g.Id, g.Interval, g.ActivityGroup, g.Metric, g.Target);
}
