using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

// Unpaged on purpose: laps belong to one activity, and import caps their number.
public sealed class GetLapsEndpoint(AppDb db) : Endpoint<IdRequest, List<LapRead>>
{
    public override void Configure() => Get("/activities/{id:int}/laps");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var activity = await db.Activities.AsNoTracking().VisibleTo(db, me).Where(a => a.Id == req.Id)
            .Select(a => new { a.UserId, a.StartTime, a.HiddenDetails }).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(await VisibleLaps.ReadAsync(db, req.Id, activity.UserId, activity.StartTime, activity.HiddenDetails, activity.UserId == me, ct), ct);
    }
}
