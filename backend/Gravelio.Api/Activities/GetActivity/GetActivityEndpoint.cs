using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class GetActivityEndpoint(AppDb db) : Endpoint<IdRequest, ActivityRead>
{
    public override void Configure() => Get("/activities/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var activity = await db.Activities.AsNoTracking().OwnedBy(User.UserId(), req.Id).Select(ActivityRead.Projection).FirstOrDefaultAsync(ct);
        if (activity is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(activity, ct);
    }
}
