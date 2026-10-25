using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class SaveTargetsEndpoint(AppDb db) : Endpoint<SaveTargetsRequest, TargetsRead>
{
    public override void Configure() => Put("/health/targets");

    public override async Task HandleAsync(SaveTargetsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var target = await db.HealthTargets.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (target is null)
        {
            target = new HealthTarget { UserId = userId };
            db.HealthTargets.Add(target);
        }
        (target.Weight, target.Steps, target.Sleep, target.Fasting, target.WaterMl, target.PoopCount) =
            (req.Weight, req.Steps, req.Sleep, req.Fasting, req.WaterMl, req.PoopCount);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("Targets were changed at the same time. Try again.", StatusCodes.Status409Conflict);
        await Send.OkAsync(TargetsRead.From(target), ct);
    }
}
