using FastEndpoints;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed record DailySleep(int? TotalSleepSeconds, int? RestingHeartRate, string? HrvStatus, decimal? AvgSkinTempDeviation);

public sealed record DailyWeight(decimal Weight, decimal? Bmi);

/// Today's numbers on the person's own calendar day: the latest sleep, weight and fast, and the day's steps, water and bowel movements.
public sealed record DailyStatsRead(DateOnly Date, DailySleep? Sleep, DailyWeight? Weight, int? Steps, FastingRead? Fasting, decimal? WaterMl, int PoopCount);

public sealed class GetDailyStatsEndpoint(AppDb db, TimeProvider time) : EndpointWithoutRequest<DailyStatsRead>
{
    public override void Configure() => Get("/health/stats/daily");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var me = User.UserId();
        var zone = await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.Timezone).FirstAsync(ct);
        var today = ActivityStats.Today(zone, time);
        // A bowel movement is stored as an instant; the day it belongs to is the local one.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var from = TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue), tz);
        var to = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), tz);

        var sleep = await db.HealthSleeps.AsNoTracking().Where(s => s.UserId == me && s.Date == today)
            .Select(s => new DailySleep(s.TotalSleepSeconds, s.RestingHeartRate, s.HrvStatus, s.AvgSkinTempDeviation)).FirstOrDefaultAsync(ct);
        var weight = await db.HealthWeights.AsNoTracking().Where(w => w.UserId == me).OrderByDescending(w => w.Date)
            .Select(w => new DailyWeight(w.Weight, w.Bmi)).FirstOrDefaultAsync(ct);
        var steps = await db.HealthSteps.AsNoTracking().Where(s => s.UserId == me && s.Date == today).Select(s => (int?)s.Steps).FirstOrDefaultAsync(ct);
        var water = await db.HealthWaters.AsNoTracking().Where(w => w.UserId == me && w.Date == today).Select(w => (decimal?)w.AmountMl).FirstOrDefaultAsync(ct);
        var poop = await db.HealthPoops.AsNoTracking().CountAsync(p => p.UserId == me && p.DateTime >= from && p.DateTime < to, ct);
        // The fast in progress, else the latest one.
        var fast = await db.HealthFastings.AsNoTracking().Where(f => f.UserId == me)
            .OrderByDescending(f => f.Status == FastingStatuses.InProgress).ThenByDescending(f => f.FastStartTime)
            .Select(FastingRead.Projection).FirstOrDefaultAsync(ct);
        await Send.OkAsync(new DailyStatsRead(today, sleep, weight, steps, fast, water, poop), ct);
    }
}
