using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class GetStreamsEndpoint(AppDb db) : Endpoint<IdRequest, List<StreamRead>>
{
    public override void Configure() => Get("/activities/{id:int}/streams");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var activity = await db.Activities.AsNoTracking().VisibleTo(db, me).Where(a => a.Id == req.Id)
            .Select(a => new { a.UserId, a.StartTime }).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(await VisibleStreams.ReadAsync(db, req.Id, activity.UserId, activity.StartTime, activity.UserId == me, ct), ct);
    }
}
