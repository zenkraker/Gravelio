using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ActivityStatsEndpoint(AppDb db, TimeProvider time) : Endpoint<ActivityStatsRequest, ActivityStatsRead>
{
    public override void Configure() => Get("/activities/stats");

    public override async Task HandleAsync(ActivityStatsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Timezone, u.FirstDayOfWeek }).FirstAsync(ct);
        var firstDay = Enum.TryParse<DayOfWeek>(me.FirstDayOfWeek, true, out var parsed) ? parsed : DayOfWeek.Monday;
        var anchor = req.Date ?? ActivityStats.Today(me.Timezone, time);

        // An unknown type name matches nothing instead of being silently ignored.
        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(req.Type))
            typeId = ActivityTypes.Names.Where(n => n.Value.Equals(req.Type.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(n => (int?)n.Key).FirstOrDefault() ?? -1;

        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId);
        if (req.Period != "lifetime")
        {
            var (start, end) = ActivityStats.Range(req.Period, anchor, firstDay, req.Year);
            // Local dates can sit up to 14 hours either side of the UTC date, so widen the window before filtering.
            var from = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(-2);
            var to = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(2);
            query = query.Where(a => a.StartTime >= from && a.StartTime < to);
        }

        var rows = await query.Select(a => new ActivityStats.Row(a.StartTime, a.Timezone, a.ActivityType, a.Distance,
            a.TotalTimerTime, a.ElevationGain, a.Calories)).ToListAsync(ct);
        await Send.OkAsync(ActivityStats.Build(rows, req.Period, anchor, firstDay, me.Timezone, typeId, req.Year), ct);
    }
}
