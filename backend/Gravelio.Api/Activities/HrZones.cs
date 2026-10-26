namespace Gravelio.Api.Activities;

public sealed record HrZoneRead(int Zone, double Percent, int Seconds, string? Range);

/// Five zones by share of max heart rate: below 60 %, 60-70, 70-80, 80-90 and 90 % and up, counted over the heart-rate samples.
public static class HrZones
{
    const int DefaultMax = 220;
    static readonly double[] Bounds = [0.6, 0.7, 0.8, 0.9];

    /// The stored max, else 220 minus the age in completed years on the person's own local date, else unknown.
    public static int? ResolveMax(int? stored, DateOnly? birthdate, DateOnly today)
    {
        if (stored is > 0) return stored;
        if (birthdate is not { } born) return null;
        var age = today.Year - born.Year - (today < born.AddYears(today.Year - born.Year) ? 1 : 0);
        return DefaultMax - age;
    }

    /// <paramref name="showRange"/> is for the owner only: the zone limits are a fixed share of their max heart rate, which they may keep private.
    public static List<HrZoneRead> Compute(IReadOnlyList<int> samples, int max, double timerSeconds, bool showRange)
    {
        if (samples.Count == 0 || max <= 0) return [];
        var limits = Bounds.Select(b => b * max).ToArray();
        var counts = new int[5];
        foreach (var hr in samples) counts[limits.Count(l => hr >= l)]++;
        string?[] ranges =
        [
            $"< {(int)limits[0]}", $"{(int)limits[0]} - {(int)limits[1] - 1}", $"{(int)limits[1]} - {(int)limits[2] - 1}",
            $"{(int)limits[2]} - {(int)limits[3] - 1}", $">= {(int)limits[3]}",
        ];
        return counts.Select((c, i) =>
        {
            var percent = Math.Round(100.0 * c / samples.Count, 2);
            return new HrZoneRead(i + 1, percent, (int)(percent / 100 * timerSeconds), showRange ? ranges[i] : null);
        }).ToList();
    }
}
