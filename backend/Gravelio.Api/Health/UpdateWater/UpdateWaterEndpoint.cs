using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdateWaterEndpoint(AppDb db) : Endpoint<UpdateWaterRequest, WaterRead>
{
    public override void Configure() => Put("/health/water/{id:int}");

    public override async Task HandleAsync(UpdateWaterRequest req, CancellationToken ct)
    {
        var water = await db.HealthWaters.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (water is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (water.Date, water.AmountMl) = (req.Date, req.AmountMl);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("There is already a water entry for that day.", StatusCodes.Status409Conflict);
        await Send.OkAsync(WaterRead.From(water), ct);
    }
}
