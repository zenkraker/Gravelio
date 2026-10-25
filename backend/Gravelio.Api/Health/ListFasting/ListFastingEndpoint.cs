using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListFastingEndpoint(AppDb db) : Endpoint<ListFastingRequest, PagedResult<FastingRead>>
{
    public override void Configure() => Get("/health/fasting");

    public override async Task HandleAsync(ListFastingRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.HealthFastings.AsNoTracking().Where(f => f.UserId == userId).OrderByDescending(f => f.FastStartTime)
            .ToPageAsync(req, page => page.Select(FastingRead.Projection), ct), ct);
    }
}
