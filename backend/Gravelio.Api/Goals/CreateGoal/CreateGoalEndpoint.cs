using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;

namespace Gravelio.Api.Goals;

public sealed class CreateGoalEndpoint(AppDb db) : Endpoint<CreateGoalRequest, GoalRead>
{
    public override void Configure() => Post("/goals");

    public override async Task HandleAsync(CreateGoalRequest req, CancellationToken ct)
    {
        var goal = new Goal { UserId = User.UserId(), Interval = req.Interval, ActivityGroup = req.ActivityGroup, Metric = req.Metric, Target = req.Target };
        db.Goals.Add(goal);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("You already have a goal for that period, activity and measure. Edit it instead.", StatusCodes.Status409Conflict);
        await Send.CreatedAtAsync<ListGoalsEndpoint>(null, GoalRead.From(goal), cancellation: ct);
    }
}
