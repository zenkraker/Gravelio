using FastEndpoints;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Goals;

public sealed class GetGoalProgressEndpoint(AppDb db, TimeProvider time) : Endpoint<GetGoalProgressRequest, List<GoalProgressRead>>
{
    public override void Configure() => Get("/goals/progress");

    public override async Task HandleAsync(GetGoalProgressRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var me = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Timezone, u.FirstDayOfWeek }).FirstAsync(ct);
        var firstDay = Enum.TryParse<DayOfWeek>(me.FirstDayOfWeek, true, out var parsed) ? parsed : DayOfWeek.Monday;
        var anchor = req.Date ?? ActivityStats.Today(me.Timezone, time);
        var goals = await db.Goals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.Id).ToListAsync(ct);
        if (goals.Count == 0)
        {
            await Send.OkAsync([], ct);
            return;
        }

        // One query for every period at once; local dates can sit up to 14 hours either side of the UTC date, so widen the window.
        var windows = goals.Select(g => GoalProgress.Window(g.Interval, anchor, firstDay)).ToList();
        var from = DateTime.SpecifyKind(windows.Min(w => w.Start).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(-2);
        var to = DateTime.SpecifyKind(windows.Max(w => w.End).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(2);
        var rows = await db.Activities.AsNoTracking().Where(a => a.UserId == userId && !a.IsHidden && a.StartTime >= from && a.StartTime < to)
            .Select(a => new ActivityStats.Row(a.StartTime, a.Timezone, a.ActivityType, a.Distance, a.TotalTimerTime, a.ElevationGain, a.Calories))
            .ToListAsync(ct);
        await Send.OkAsync(goals.Select(g => GoalProgress.Compute(g, rows, anchor, firstDay, me.Timezone)).ToList(), ct);
    }
}
