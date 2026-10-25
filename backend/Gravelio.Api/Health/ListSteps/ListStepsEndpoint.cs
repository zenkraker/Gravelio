using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListStepsEndpoint(AppDb db) : Endpoint<ListStepsRequest, PagedResult<StepRead>>
{
    public override void Configure() => Get("/health/steps");

    public override async Task HandleAsync(ListStepsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.HealthSteps.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.Date)
            .ToPageAsync(req, page => page.Select(StepRead.Projection), ct), ct);
    }
}
