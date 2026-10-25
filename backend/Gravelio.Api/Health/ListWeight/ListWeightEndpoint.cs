using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListWeightEndpoint(AppDb db) : Endpoint<ListWeightRequest, PagedResult<WeightRead>>
{
    public override void Configure() => Get("/health/weight");

    public override async Task HandleAsync(ListWeightRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.HealthWeights.AsNoTracking().Where(w => w.UserId == userId).OrderByDescending(w => w.Date)
            .ToPageAsync(req, page => page.Select(WeightRead.Projection), ct), ct);
    }
}
