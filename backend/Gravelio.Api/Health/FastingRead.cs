using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record FastingRead(int Id, DateTime FastStartTime, DateTime? FastEndTime, int? TargetDurationSeconds, int? ActualDurationSeconds,
    string? FastingType, string Status, string? Notes, string? Source)
{
    public static readonly Expression<Func<HealthFasting, FastingRead>> Projection =
        f => new(f.Id, f.FastStartTime, f.FastEndTime, f.TargetDurationSeconds, f.ActualDurationSeconds, f.FastingType, f.Status, f.Notes, f.Source);

    static readonly Func<HealthFasting, FastingRead> Compiled = Projection.Compile();

    public static FastingRead From(HealthFasting f) => Compiled(f);
}

public sealed record FastingStatsRead(int TotalFasts, int CurrentStreak, int LongestStreak, int? AvgDurationSeconds, int TotalFastingSeconds,
    double CompletionRate);
