using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Goals;

public sealed class UpdateGoalEndpoint(AppDb db) : Endpoint<UpdateGoalRequest, GoalRead>
{
    public override void Configure() => Put("/goals/{id:int}");

    public override async Task HandleAsync(UpdateGoalRequest req, CancellationToken ct)
    {
        var goal = await db.Goals.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (goal is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (goal.Interval, goal.ActivityGroup, goal.Metric, goal.Target) = (req.Interval, req.ActivityGroup, req.Metric, req.Target);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("You already have a goal for that period, activity and measure.", StatusCodes.Status409Conflict);
        await Send.OkAsync(GoalRead.From(goal), ct);
    }
}
