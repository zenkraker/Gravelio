namespace Gravelio.Api.Goals;

public sealed record CreateGoalRequest(string Interval, string ActivityGroup, string Metric, int Target);
