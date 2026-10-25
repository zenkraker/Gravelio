using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListPoopEndpoint(AppDb db) : Endpoint<ListPoopRequest, PagedResult<PoopRead>>
{
    public override void Configure() => Get("/health/poop");

    public override async Task HandleAsync(ListPoopRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.HealthPoops.AsNoTracking().Where(p => p.UserId == userId).OrderByDescending(p => p.DateTime)
            .ToPageAsync(req, page => page.Select(PoopRead.Projection), ct), ct);
    }
}
