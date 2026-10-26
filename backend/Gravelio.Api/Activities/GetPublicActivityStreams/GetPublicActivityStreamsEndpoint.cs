using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Settings;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// The streams behind a public link, masked exactly like any other viewer's by <see cref="VisibleStreams"/>.
public sealed class GetPublicActivityStreamsEndpoint(AppDb db) : Endpoint<IdRequest, List<StreamRead>>
{
    public override void Configure()
    {
        Get("/public/activities/{id:int}/streams");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var settings = await ServerSettingsStore.GetAsync(db, ct);
        var activity = settings.PublicShareableLinks
            ? await db.Activities.AsNoTracking().PubliclyLinkable().Where(a => a.Id == req.Id).Select(a => new { a.UserId, a.StartTime }).FirstOrDefaultAsync(ct)
            : null;
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(await VisibleStreams.ReadAsync(db, req.Id, activity.UserId, activity.StartTime, isOwner: false, ct), ct);
    }
}
