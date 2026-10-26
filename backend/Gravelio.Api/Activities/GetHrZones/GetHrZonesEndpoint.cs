using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

// Unpaged on purpose: always five zones.
/// Time in each heart-rate zone, worked out on read from the heart-rate stream the viewer may see. Without a visible stream, or without a
/// max heart rate (stored or from a birthdate), the list is empty.
public sealed class GetHrZonesEndpoint(AppDb db, TimeProvider time) : Endpoint<IdRequest, List<HrZoneRead>>
{
    public override void Configure() => Get("/activities/{id:int}/hr-zones");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var activity = await db.Activities.AsNoTracking().VisibleTo(db, me).Where(a => a.Id == req.Id)
            .Select(a => new { a.UserId, a.StartTime, a.HiddenDetails, a.TotalTimerTime }).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var owner = await db.Users.AsNoTracking().Where(u => u.Id == activity.UserId).Select(u => new { u.MaxHeartRate, u.Birthdate, u.Timezone }).FirstAsync(ct);
        var max = HrZones.ResolveMax(owner.MaxHeartRate, owner.Birthdate, ActivityStats.Today(owner.Timezone, time));

        var stream = (await VisibleStreams.ReadAsync(db, req.Id, activity.UserId, activity.StartTime, activity.HiddenDetails, activity.UserId == me, ct))
            .FirstOrDefault(s => s.StreamType == StreamTypes.Hr);
        if (stream is null || max is null)
        {
            await Send.OkAsync([], ct);
            return;
        }
        var samples = stream.Waypoints.EnumerateArray()
            .Where(w => w.TryGetProperty("hr", out var hr) && hr.ValueKind == JsonValueKind.Number)
            .Select(w => w.GetProperty("hr").GetInt32()).ToList();
        await Send.OkAsync(HrZones.Compute(samples, max.Value, activity.TotalTimerTime, showRange: activity.UserId == me), ct);
    }
}
