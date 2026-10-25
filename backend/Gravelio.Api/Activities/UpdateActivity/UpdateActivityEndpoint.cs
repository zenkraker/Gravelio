using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class UpdateActivityEndpoint(AppDb db) : Endpoint<UpdateActivityRequest, ActivityRead>
{
    public override void Configure() => Put("/activities/{id:int}");

    public override async Task HandleAsync(UpdateActivityRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var activity = await db.Activities.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (activity.Name, activity.Description, activity.ActivityType, activity.Visibility) = (req.Name, req.Description, req.ActivityType, req.Visibility);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ActivityRead.From(activity), ct);
    }
}
