using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Gravelio.Api.Settings;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// Anonymous read of one activity through a share link. Answers 404 for everything it will not show (links off, activity not public,
/// owner deactivated, no such id), so a visitor cannot tell which ids exist.
public sealed class GetPublicActivityEndpoint(AppDb db) : Endpoint<IdRequest, GetPublicActivityResponse>
{
    public override void Configure()
    {
        Get("/public/activities/{id:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var settings = await ServerSettingsStore.GetAsync(db, ct);
        var activity = settings.PublicShareableLinks
            ? await db.Activities.AsNoTracking().PubliclyLinkable().Where(a => a.Id == req.Id).Select(ActivityRead.Projection).FirstOrDefaultAsync(ct)
            : null;
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var masked = PrivacyMask.Apply(activity, await PrivacyMask.ForOwnerAsync(db, activity.Owner.Id, ct), isOwner: false);
        await Send.OkAsync(GetPublicActivityResponse.From(masked, settings.PublicShareableLinksUserInfo ? masked.Owner.Name : null), ct);
    }
}
