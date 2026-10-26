using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

// Unpaged on purpose: laps belong to one activity, and import caps their number.
/// The laps behind a public link, masked exactly like any other viewer's by <see cref="VisibleLaps"/>.
public sealed class GetPublicActivityLapsEndpoint(AppDb db) : Endpoint<IdRequest, List<LapRead>>
{
    public override void Configure()
    {
        Get("/public/activities/{id:int}/laps");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var settings = await ServerSettingsStore.GetAsync(db, ct);
        var activity = settings.PublicShareableLinks
            ? await db.Activities.AsNoTracking().PubliclyLinkable().Where(a => a.Id == req.Id).Select(a => new { a.UserId, a.StartTime, a.HiddenDetails }).FirstOrDefaultAsync(ct)
            : null;
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(await VisibleLaps.ReadAsync(db, req.Id, activity.UserId, activity.StartTime, activity.HiddenDetails, isOwner: false, ct), ct);
    }
}
