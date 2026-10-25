using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class FastingStatsEndpoint(AppDb db, TimeProvider time) : EndpointWithoutRequest<FastingStatsRead>
{
    public override void Configure() => Get("/health/fasting/stats");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        var started = await db.HealthFastings.CountAsync(f => f.UserId == userId, ct);
        var completed = await db.HealthFastings.Where(f => f.UserId == userId && f.Status == FastingStatuses.Completed).ToListAsync(ct);
        int totalSeconds = completed.Sum(f => f.ActualDurationSeconds ?? 0);
        int? average = completed.Count == 0 ? null : totalSeconds / completed.Count;
        double rate = started == 0 ? 0 : Math.Round((double)completed.Count / started * 100, 1);

        var dates = completed.Where(f => f.FastEndTime is not null).Select(f => DateOnly.FromDateTime(f.FastEndTime!.Value)).Distinct()
            .OrderBy(d => d).ToList();
        int longest = 0, run = 0;
        DateOnly? previous = null;
        foreach (var date in dates)
        {
            run = previous is not null && date == previous.Value.AddDays(1) ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = date;
        }
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        int current = dates.Count > 0 && dates[^1] >= today.AddDays(-1) ? run : 0;

        await Send.OkAsync(new FastingStatsRead(completed.Count, current, longest, average, totalSeconds, rate), ct);
    }
}
