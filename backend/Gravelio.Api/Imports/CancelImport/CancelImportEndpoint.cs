using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Imports;

/// Stops a queued or running import. Files already imported stay; the job notices before its next file.
public sealed class CancelImportEndpoint(AppDb db, TimeProvider time, IOptions<ImportOptions> options) : Endpoint<IdRequest, ImportJobRead>
{
    public override void Configure() => Post("/activities/import/jobs/{id:int}/cancel");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var job = await db.ImportJobs.OwnedBy(me, req.Id).FirstOrDefaultAsync(ct);
        if (job is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!ImportStatuses.IsActive(job.Status)) ThrowError("This import has already finished.", StatusCodes.Status409Conflict);

        var wasQueued = job.Status == ImportStatuses.Queued;
        (job.Status, job.FinishedAt) = (ImportStatuses.Cancelled, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        // A queued job has not opened its zip yet; a running one deletes it when it stops.
        if (wasQueued) ImportFiles.TryDelete(ImportFiles.PathFor(options.Value, job.Id));
        await Send.OkAsync(ImportJobRead.From(job), ct);
    }
}
