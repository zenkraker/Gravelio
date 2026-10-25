using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdatePoopEndpoint(AppDb db) : Endpoint<UpdatePoopRequest, PoopRead>
{
    public override void Configure() => Put("/health/poop/{id:int}");

    public override async Task HandleAsync(UpdatePoopRequest req, CancellationToken ct)
    {
        var poop = await db.HealthPoops.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (poop is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (poop.DateTime, poop.BristolType, poop.Color, poop.Notes) =
            (HealthTime.Utc(req.DateTime), req.BristolType, req.Color, req.Notes);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(PoopRead.From(poop), ct);
    }
}
