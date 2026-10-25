using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class DeleteFastingEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/health/fasting/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (await db.HealthFastings.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
