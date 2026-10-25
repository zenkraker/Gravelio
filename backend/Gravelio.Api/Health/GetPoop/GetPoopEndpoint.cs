using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class GetPoopEndpoint(AppDb db) : Endpoint<IdRequest, PoopRead>
{
    public override void Configure() => Get("/health/poop/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var poop = await db.HealthPoops.AsNoTracking().OwnedBy(User.UserId(), req.Id).Select(PoopRead.Projection).FirstOrDefaultAsync(ct);
        if (poop is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(poop, ct);
    }
}
