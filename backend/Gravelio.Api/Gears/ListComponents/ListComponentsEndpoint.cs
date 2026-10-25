using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListComponentsEndpoint(AppDb db) : Endpoint<IdRequest, List<ComponentRead>>
{
    public override void Configure() => Get("/gears/{id:int}/components");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (!await db.Gears.OwnedBy(User.UserId(), req.Id).AnyAsync(ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var components = await db.GearComponents.AsNoTracking().Where(c => c.GearId == req.Id).OrderBy(c => c.Type).ThenBy(c => c.PurchaseDate).ToListAsync(ct);
        var used = await ComponentUsage.UsedAsync(db, User.UserId(), req.Id, components, ct);
        await Send.OkAsync(components.Select(c => ComponentRead.From(c, used[c.Id])).ToList(), ct);
    }
}
