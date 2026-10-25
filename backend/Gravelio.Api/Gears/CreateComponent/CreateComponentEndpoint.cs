using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class CreateComponentEndpoint(AppDb db) : Endpoint<CreateComponentRequest, ComponentRead>
{
    public override void Configure() => Post("/gears/{id:int}/components");

    public override async Task HandleAsync(CreateComponentRequest req, CancellationToken ct)
    {
        var gear = await db.Gears.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (gear is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!GearTypes.AllowsComponent(gear.GearType, req.Type)) ThrowError(r => r.Type, "This gear doesn't support that component type", StatusCodes.Status400BadRequest);
        var component = new GearComponent
        {
            GearId = gear.Id,
            Type = req.Type,
            Brand = req.Brand,
            Model = req.Model,
            PurchaseDate = req.PurchaseDate,
            RetiredDate = req.RetiredDate,
            // A retired component is no longer in use, whatever the caller sent.
            Active = req.Active && req.RetiredDate is null,
            ExpectedDistance = req.ExpectedDistance,
            PurchaseValue = req.PurchaseValue,
        };
        db.GearComponents.Add(component);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<ListComponentsEndpoint>(new { id = gear.Id }, await ComponentUsage.ReadAsync(db, User.UserId(), component, ct), cancellation: ct);
    }
}
