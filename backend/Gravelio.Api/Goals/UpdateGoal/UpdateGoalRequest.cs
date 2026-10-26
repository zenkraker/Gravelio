namespace Gravelio.Api.Goals;

public sealed record UpdateGoalRequest(int Id, string Interval, string ActivityGroup, string Metric, int Target);
