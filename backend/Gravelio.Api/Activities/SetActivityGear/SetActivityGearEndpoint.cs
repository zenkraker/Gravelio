using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class SetActivityGearEndpoint(AppDb db) : Endpoint<SetActivityGearRequest, ActivityRead>
{
    public override void Configure() => Put("/activities/{id:int}/gear");

    public override async Task HandleAsync(SetActivityGearRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var activity = await db.Activities.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // Another user's gear must look exactly like gear that doesn't exist.
        if (req.GearId is { } gearId && !await db.Gears.AnyAsync(g => g.Id == gearId && g.UserId == userId, ct))
            ThrowError(r => r.GearId, "Gear not found", StatusCodes.Status404NotFound);
        activity.GearId = req.GearId;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ActivityRead.From(activity), ct);
    }
}
