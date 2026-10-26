using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Goals;

// Unpaged on purpose: the unique index on (user, interval, group, metric) caps a user's goals at 4 x 6 x 5 = 120.
public sealed class ListGoalsEndpoint(AppDb db) : EndpointWithoutRequest<List<GoalRead>>
{
    public override void Configure() => Get("/goals");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.Goals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.Id)
            .Select(g => new GoalRead(g.Id, g.Interval, g.ActivityGroup, g.Metric, g.Target)).ToListAsync(ct), ct);
    }
}
