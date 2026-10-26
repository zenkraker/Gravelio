namespace Gravelio.Api.Goals;

public sealed class GetGoalProgressRequest
{
    /// The caller's local calendar date, deciding which day, week, month and year are current; defaults to today in the user's zone.
    public DateOnly? Date { get; init; }
}
