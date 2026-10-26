using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Imports;

public sealed class ListImportsRequest : PagedRequest;

public sealed class ListImportsValidator : PagedValidator<ListImportsRequest>;

public sealed class ListImportsEndpoint(AppDb db) : Endpoint<ListImportsRequest, PagedResult<ImportJobRead>>
{
    public override void Configure() => Get("/activities/import/jobs");

    public override async Task HandleAsync(ListImportsRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var page = await db.ImportJobs.AsNoTracking().Where(j => j.UserId == me).OrderByDescending(j => j.Id).ToPageAsync(req, p => p, ct);
        await Send.OkAsync(new PagedResult<ImportJobRead>([.. page.Items.Select(ImportJobRead.From)], page.Total, page.Page, page.PageSize), ct);
    }
}
