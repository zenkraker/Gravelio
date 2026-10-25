using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class DeleteComponentEndpoint(AppDb db) : Endpoint<DeleteComponentRequest>
{
    public override void Configure() => Delete("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(DeleteComponentRequest req, CancellationToken ct)
    {
        if (await db.GearComponents.OwnedBy(User.UserId(), req.Id, req.ComponentId).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
