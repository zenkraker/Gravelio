using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class GetActivityEndpoint(AppDb db) : Endpoint<IdRequest, ActivityRead>
{
    public override void Configure() => Get("/activities/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var activity = await db.Activities.AsNoTracking().VisibleTo(db, me).Where(a => a.Id == req.Id)
            .Select(ActivityRead.Projection).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (activity.Owner.Id != me)
            activity = PrivacyMask.Apply(activity, await PrivacyMask.ForOwnerAsync(db, activity.Owner.Id, ct), false);
        ETags.Set(HttpContext.Response, await db.Activities.Where(a => a.Id == req.Id).Select(a => EF.Property<uint>(a, "xmin")).FirstAsync(ct));
        await Send.OkAsync(activity, ct);
    }
}
