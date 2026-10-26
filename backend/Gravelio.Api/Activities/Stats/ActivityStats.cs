using System.Collections.Concurrent;

namespace Gravelio.Api.Activities;

public sealed record StatsBucket(DateOnly Start, DateOnly End, double Distance, double Duration, double ElevationGain, double Calories, int Count);

public sealed record StatsType(int ActivityType, string Name, double Distance, double Duration, double ElevationGain, double Calories, int Count);

public sealed record ActivityStatsRead(double TotalDistance, double TotalDuration, double TotalElevationGain, double TotalCalories,
    int ActivityCount, List<StatsBucket> Breakdown, List<StatsType> TypeBreakdown);

/// Aggregates a user's activities into the buckets a statistics page shows. Pure so it can be unit-tested; the endpoint
/// only fetches the rows. Buckets are decided on each activity's local wall clock, not on the stored UTC instant.
public static class ActivityStats
{
    public sealed record Row(DateTime StartTime, string? Timezone, int ActivityType, int Distance, double TimerTime, int? ElevationGain,
        int? Calories);

    static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new(StringComparer.OrdinalIgnoreCase);

    static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        return Zones.GetOrAdd(id, key =>
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(key);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        });
    }

    /// The local calendar date an activity started on, in the zone it was recorded in.
    public static DateOnly LocalDate(Row row, string fallbackTimezone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc),
            Zone(row.Timezone ?? fallbackTimezone)));

    public static DateOnly Today(string timezone, TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(time.GetUtcNow().UtcDateTime, Zone(timezone)));

    /// The local date range a period covers; lifetime is unbounded.
    public static (DateOnly Start, DateOnly End) Range(string period, DateOnly anchor, DayOfWeek firstDay, int? year)
    {
        switch (period)
        {
            case "week":
                var start = anchor.AddDays(-(((int)anchor.DayOfWeek - (int)firstDay + 7) % 7));
                return (start, start.AddDays(7));
            case "month":
                var month = new DateOnly(anchor.Year, anchor.Month, 1);
                return (month, month.AddMonths(1));
            case "year":
                var y = year ?? anchor.Year;
                return (new DateOnly(y, 1, 1), new DateOnly(y + 1, 1, 1));
            default:
                return (DateOnly.MinValue, DateOnly.MaxValue);
        }
    }

    public static ActivityStatsRead Build(IReadOnlyList<Row> rows, string period, DateOnly anchor, DayOfWeek firstDay,
        string fallbackTimezone, int? typeId, int? year = null)
    {
        var (start, end) = Range(period, anchor, firstDay, year);
        var lifetime = period == "lifetime";
        var items = rows
            .Where(r => typeId is null || r.ActivityType == typeId)
            .Select(r => (Row: r, Date: LocalDate(r, fallbackTimezone)))
            .Where(x => lifetime || (x.Date >= start && x.Date < end))
            .ToList();

        var buckets = new List<(DateOnly Start, DateOnly End)>();
        switch (period)
        {
            case "week":
                for (var day = start; day < end; day = day.AddDays(1)) buckets.Add((day, day.AddDays(1)));
                break;
            case "month":
                // The first bucket is the days left in the week the month starts in, the rest are full weeks.
                var leading = ((int)start.DayOfWeek - (int)firstDay + 7) % 7;
                for (var cursor = start; cursor < end;)
                {
                    var next = cursor.AddDays(cursor == start ? 7 - leading : 7);
                    if (next > end) next = end;
                    buckets.Add((cursor, next));
                    cursor = next;
                }
                break;
            case "year":
                for (var month = 1; month <= 12; month++)
                {
                    var first = new DateOnly(start.Year, month, 1);
                    buckets.Add((first, first.AddMonths(1)));
                }
                break;
            default:
                foreach (var y in items.Select(x => x.Date.Year).Distinct().OrderByDescending(y => y))
                    buckets.Add((new DateOnly(y, 1, 1), new DateOnly(y + 1, 1, 1)));
                break;
        }

        static StatsBucket Sum((DateOnly Start, DateOnly End) bucket, IEnumerable<(Row Row, DateOnly Date)> all)
        {
            var inBucket = all.Where(x => x.Date >= bucket.Start && x.Date < bucket.End).ToList();
            return new StatsBucket(bucket.Start, bucket.End, inBucket.Sum(x => (double)x.Row.Distance),
                inBucket.Sum(x => x.Row.TimerTime), inBucket.Sum(x => (double)(x.Row.ElevationGain ?? 0)),
                inBucket.Sum(x => (double)(x.Row.Calories ?? 0)), inBucket.Count);
        }

        var breakdown = buckets.Select(b => Sum(b, items)).ToList();
        var types = items.GroupBy(x => x.Row.ActivityType)
            .Select(g => new StatsType(g.Key, ActivityTypes.Names.GetValueOrDefault(g.Key, "Workout"),
                g.Sum(x => (double)x.Row.Distance), g.Sum(x => x.Row.TimerTime),
                g.Sum(x => (double)(x.Row.ElevationGain ?? 0)), g.Sum(x => (double)(x.Row.Calories ?? 0)), g.Count()))
            .OrderByDescending(t => t.Count).ThenBy(t => t.ActivityType).ToList();

        return new ActivityStatsRead(items.Sum(x => (double)x.Row.Distance), items.Sum(x => x.Row.TimerTime),
            items.Sum(x => (double)(x.Row.ElevationGain ?? 0)), items.Sum(x => (double)(x.Row.Calories ?? 0)), items.Count,
            breakdown, types);
    }
}
