using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListWaterEndpoint(AppDb db) : Endpoint<ListWaterRequest, PagedResult<WaterRead>>
{
    public override void Configure() => Get("/health/water");

    public override async Task HandleAsync(ListWaterRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.HealthWaters.AsNoTracking().Where(w => w.UserId == userId).OrderByDescending(w => w.Date)
            .ToPageAsync(req, page => page.Select(WaterRead.Projection), ct), ct);
    }
}
