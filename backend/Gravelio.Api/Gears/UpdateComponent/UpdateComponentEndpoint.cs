using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class UpdateComponentEndpoint(AppDb db) : Endpoint<UpdateComponentRequest, ComponentRead>
{
    public override void Configure() => Put("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(UpdateComponentRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var component = await db.GearComponents.Include(c => c.Gear).OwnedBy(userId, req.Id, req.ComponentId).FirstOrDefaultAsync(ct);
        if (component is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!GearTypes.AllowsComponent(component.Gear.GearType, req.Type)) ThrowError(r => r.Type, "This gear doesn't support that component type", StatusCodes.Status400BadRequest);
        (component.Type, component.Brand, component.Model, component.PurchaseDate, component.RetiredDate, component.Active, component.ExpectedDistance, component.PurchaseValue) =
            (req.Type, req.Brand, req.Model, req.PurchaseDate, req.RetiredDate, req.Active && req.RetiredDate is null, req.ExpectedDistance, req.PurchaseValue);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(await ComponentUsage.ReadAsync(db, userId, component, ct), ct);
    }
}
