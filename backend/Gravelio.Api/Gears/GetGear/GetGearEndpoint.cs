using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class GetGearEndpoint(AppDb db) : Endpoint<IdRequest, GearRead>
{
    public override void Configure() => Get("/gears/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var gear = await db.Gears.AsNoTracking().OwnedBy(User.UserId(), req.Id).ToRead(db).FirstOrDefaultAsync(ct);
        if (gear is null) await Send.NotFoundAsync(ct);
        else
        {
            ETags.Set(HttpContext.Response, await db.Gears.Where(g => g.Id == req.Id).Select(g => EF.Property<uint>(g, "xmin")).FirstAsync(ct));
            await Send.OkAsync(gear, ct);
        }
    }
}
