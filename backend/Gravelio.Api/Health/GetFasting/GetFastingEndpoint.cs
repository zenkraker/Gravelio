using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class GetFastingEndpoint(AppDb db) : Endpoint<IdRequest, FastingRead>
{
    public override void Configure() => Get("/health/fasting/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var fast = await db.HealthFastings.AsNoTracking().OwnedBy(User.UserId(), req.Id).Select(FastingRead.Projection).FirstOrDefaultAsync(ct);
        if (fast is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(fast, ct);
    }
}
