using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

/// Adds to the day's total: a second post for the same date accumulates, unlike weight/steps.
public sealed class CreateWaterEndpoint(AppDb db) : Endpoint<CreateWaterRequest, WaterRead>
{
    public override void Configure() => Post("/health/water");

    public override async Task HandleAsync(CreateWaterRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var existing = await db.HealthWaters.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId && w.Date == req.Date, ct);
        if (existing is null)
        {
            var water = new HealthWater { UserId = userId, Date = req.Date, AmountMl = req.AmountMl };
            db.HealthWaters.Add(water);
            if (!await db.TrySaveUniqueAsync(ct)) ThrowError("A water entry for this day was saved at the same time. Try again.", StatusCodes.Status409Conflict);
            await Send.CreatedAtAsync<ListWaterEndpoint>(null, WaterRead.From(water), cancellation: ct);
            return;
        }
        // One atomic increment, so two concurrent adds cannot overwrite each other.
        var updated = await db.HealthWaters.Where(w => w.Id == existing.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.AmountMl, w => w.AmountMl + req.AmountMl), ct);
        if (updated == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(WaterRead.From(await db.HealthWaters.AsNoTracking().FirstAsync(w => w.Id == existing.Id, ct)), ct);
    }
}
