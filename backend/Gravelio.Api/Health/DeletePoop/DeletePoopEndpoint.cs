using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class DeletePoopEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/health/poop/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (await db.HealthPoops.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
