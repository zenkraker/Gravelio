using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class DeleteActivityEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/activities/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (await db.Activities.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
