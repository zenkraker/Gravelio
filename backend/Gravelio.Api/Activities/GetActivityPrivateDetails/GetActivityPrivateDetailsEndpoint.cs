using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// Owner only: another user's activity answers 404, so notes can never leak through a route that other viewers can reach.
public sealed class GetActivityPrivateDetailsEndpoint(AppDb db) : Endpoint<IdRequest, GetActivityPrivateDetailsResponse>
{
    public override void Configure() => Get("/activities/{id:int}/private");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var activity = await db.Activities.AsNoTracking().OwnedBy(User.UserId(), req.Id)
            .Select(a => new { a.PrivateNotes, a.WorkoutFeeling, a.WorkoutRpe, a.HiddenDetails }).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(new GetActivityPrivateDetailsResponse(activity.PrivateNotes, activity.WorkoutFeeling, activity.WorkoutRpe, HiddenDetailNames.ToNames(activity.HiddenDetails)), ct);
    }
}
