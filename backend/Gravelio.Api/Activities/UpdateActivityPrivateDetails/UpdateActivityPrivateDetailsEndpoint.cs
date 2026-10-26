using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// The owner-only part of an activity: notes, how it felt, and what to hide from other viewers.
public sealed class UpdateActivityPrivateDetailsEndpoint(AppDb db) : Endpoint<UpdateActivityPrivateDetailsRequest, UpdateActivityPrivateDetailsResponse>
{
    public override void Configure() => Put("/activities/{id:int}/private");

    public override async Task HandleAsync(UpdateActivityPrivateDetailsRequest req, CancellationToken ct)
    {
        var activity = await db.Activities.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (activity.PrivateNotes, activity.WorkoutFeeling, activity.WorkoutRpe, activity.HiddenDetails, activity.IsHidden) =
            (string.IsNullOrWhiteSpace(req.PrivateNotes) ? null : req.PrivateNotes, req.WorkoutFeeling, req.WorkoutRpe, HiddenDetailNames.FromNames(req.HiddenDetails), req.IsHidden);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(new UpdateActivityPrivateDetailsResponse(activity.PrivateNotes, activity.WorkoutFeeling, activity.WorkoutRpe, HiddenDetailNames.ToNames(activity.HiddenDetails), activity.IsHidden), ct);
    }
}
