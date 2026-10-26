using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Imports;

public sealed class GetImportEndpoint(AppDb db) : Endpoint<IdRequest, ImportJobRead>
{
    public override void Configure() => Get("/activities/import/jobs/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var job = await db.ImportJobs.AsNoTracking().OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (job is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(ImportJobRead.From(job), ct);
    }
}
