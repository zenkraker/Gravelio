using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class UpdateGearEndpoint(AppDb db) : Endpoint<UpdateGearRequest, GearRead>
{
    public override void Configure() => Put("/gears/{id:int}");

    public override async Task HandleAsync(UpdateGearRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var gear = await db.Gears.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (gear is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (req.GearType != gear.GearType)
        {
            // Components and default-gear slots are tied to the gear type, so a type change must not strand them.
            var componentTypes = await db.GearComponents.Where(c => c.GearId == gear.Id).Select(c => c.Type).ToListAsync(ct);
            if (componentTypes.Any(t => !GearTypes.AllowsComponent(req.GearType, t)))
                ThrowError("This gear has components its new type doesn't support. Remove them first.", StatusCodes.Status400BadRequest);
            if (await db.DefaultGears.AnyAsync(d => d.GearId == gear.Id, ct))
                ThrowError("Clear this gear's default-gear settings before changing its type.", StatusCodes.Status400BadRequest);
        }
        (gear.Nickname, gear.Brand, gear.Model, gear.GearType, gear.Active, gear.InitialDistance, gear.PurchaseValue) =
            (req.Nickname.Trim(), req.Brand, req.Model, req.GearType, req.Active, req.InitialDistance, req.PurchaseValue);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(await db.Gears.AsNoTracking().Where(g => g.Id == gear.Id).ToRead(db).FirstAsync(ct), ct);
    }
}
