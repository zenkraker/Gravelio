using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ListSleepEndpoint(AppDb db) : Endpoint<ListSleepRequest, PagedResult<SleepRead>>
{
    public override void Configure() => Get("/health/sleep");

    public override async Task HandleAsync(ListSleepRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var page = await db.HealthSleeps.AsNoTracking().Where(s => s.UserId == userId).OrderByDescending(s => s.Date)
            .ToPageAsync(req, page => page, ct);
        await Send.OkAsync(new PagedResult<SleepRead>(page.Items.Select(SleepRead.From).ToList(), page.Total, page.Page, page.PageSize), ct);
    }
}
