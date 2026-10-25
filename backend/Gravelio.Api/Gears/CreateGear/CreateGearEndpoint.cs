using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class CreateGearEndpoint(AppDb db, TimeProvider time) : Endpoint<CreateGearRequest, GearRead>
{
    public override void Configure() => Post("/gears");

    public override async Task HandleAsync(CreateGearRequest req, CancellationToken ct)
    {
        var gear = new Gear
        {
            UserId = User.UserId(),
            Nickname = req.Nickname.Trim(),
            Brand = req.Brand,
            Model = req.Model,
            GearType = req.GearType,
            Active = req.Active,
            InitialDistance = req.InitialDistance,
            PurchaseValue = req.PurchaseValue,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Gears.Add(gear);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<GetGearEndpoint>(new { id = gear.Id }, await db.Gears.AsNoTracking().Where(g => g.Id == gear.Id).ToRead(db).FirstAsync(ct), cancellation: ct);
    }
}
