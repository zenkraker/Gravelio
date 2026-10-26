using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ListActivitiesEndpoint(AppDb db) : Endpoint<ListActivitiesRequest, PagedResult<ActivityRead>>
{
    public override void Configure() => Get("/activities");

    public override async Task HandleAsync(ListActivitiesRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId);
        if (req.Type is { } type) query = query.Where(a => a.ActivityType == type);
        if (req.GearId is { } gearId) query = query.Where(a => a.GearId == gearId);
        if (!string.IsNullOrWhiteSpace(req.Q)) query = query.Where(a => EF.Functions.ILike(a.Name!, QueryExtensions.ContainsPattern(req.Q.Trim()), "\\"));
        if (req.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => AppDb.LocalTime(a.Timezone ?? a.User.Timezone, a.StartTime) >= start);
        }
        if (req.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => AppDb.LocalTime(a.Timezone ?? a.User.Timezone, a.StartTime) < end);
        }
        await Send.OkAsync(await query.Sorted(req.Sort, req.Order == "desc").ToPageAsync(req, page => page.Select(ActivityRead.Projection), ct), ct);
    }
}
