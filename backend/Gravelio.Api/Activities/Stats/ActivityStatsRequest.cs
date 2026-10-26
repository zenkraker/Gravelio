using FastEndpoints;
using FluentValidation;

namespace Gravelio.Api.Activities;

public sealed class ActivityStatsRequest
{
    /// week, month, year or lifetime.
    public string Period { get; init; } = "week";

    /// The caller's local calendar date, deciding which week or month is current.
    public DateOnly? Date { get; init; }

    /// Target year for a yearly summary; defaults to the anchor date's year.
    public int? Year { get; init; }

    /// Filters the summary by activity type name, e.g. "Ride".
    public string? Type { get; init; }
}

public sealed class ActivityStatsValidator : Validator<ActivityStatsRequest>
{
    public ActivityStatsValidator(TimeProvider time)
    {
        RuleFor(x => x.Period).Must(p => p is "week" or "month" or "year" or "lifetime")
            .WithMessage("Period must be week, month, year or lifetime");
        // Widened by a year so a caller just past midnight on 1 January in UTC+N is not rejected.
        RuleFor(x => x.Year).Must(y => y is >= 1900 && y <= time.GetUtcNow().Year + 1).When(x => x.Year is not null)
            .WithMessage("Year is out of range");
    }
}
