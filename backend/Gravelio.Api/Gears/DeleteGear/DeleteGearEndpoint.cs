using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class DeleteGearEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/gears/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        // The database unlinks the gear's activities and removes its components and default-gear slots.
        if (await db.Gears.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
