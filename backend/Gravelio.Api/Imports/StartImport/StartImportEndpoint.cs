using System.IO.Compression;
using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Imports;

/// Takes a zip of GPX, TCX and FIT files and queues a background import of it; <c>GET /activities/import/jobs/{id}</c> shows the progress.
public sealed class StartImportEndpoint(AppDb db, TimeProvider time, IOptions<ImportOptions> options) : Endpoint<StartImportRequest, ImportJobRead>
{
    public const long MaxUploadBytes = 500 * 1024 * 1024;

    public override void Configure()
    {
        Post("/activities/import/zip");
        AllowFileUploads();
        Options(o => o.WithMetadata(UploadLimits.For(MaxUploadBytes)).RequireRateLimiting(RateLimiting.Upload));
    }

    public override async Task HandleAsync(StartImportRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var settings = options.Value;
        if (req.IdempotencyKey is { Length: > 0 } key
            && await db.ImportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.UserId == me && j.IdempotencyKey == key, ct) is { } earlier)
        {
            await Send.OkAsync(ImportJobRead.From(earlier), ct);
            return;
        }
        if (await db.ImportJobs.AnyAsync(j => j.UserId == me && (j.Status == ImportStatuses.Queued || j.Status == ImportStatuses.Running), ct))
            ThrowError("An import is already running. Wait for it to finish or cancel it first.", StatusCodes.Status409Conflict);

        Directory.CreateDirectory(settings.Directory);
        var temp = Path.Combine(settings.Directory, $"{Guid.NewGuid():N}.upload");
        int files;
        try
        {
            await using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var source = req.File.OpenReadStream())
                await source.CopyToAsync(target, ct);

            // Only the directory is read here; the job reads the files, counting what it really reads.
            try
            {
                using var archive = ZipFile.OpenRead(temp);
                if (archive.Entries.Count > settings.MaxFiles) ThrowError($"The zip has more than {settings.MaxFiles} files.", StatusCodes.Status422UnprocessableEntity);
                files = archive.Entries.Count(ImportFiles.IsActivityFile);
            }
            catch (InvalidDataException)
            {
                ThrowError("That is not a readable zip file.", StatusCodes.Status422UnprocessableEntity);
                return;
            }
            if (files == 0) ThrowError("The zip holds no .gpx, .tcx or .fit files.", StatusCodes.Status422UnprocessableEntity);

            var job = new ImportJob
            {
                UserId = me,
                FileName = Path.GetFileName(req.File.FileName),
                IdempotencyKey = req.IdempotencyKey is { Length: > 0 } k ? k : null,
                TotalFiles = files,
                CreatedAt = time.GetUtcNow().UtcDateTime,
            };
            db.ImportJobs.Add(job);
            // The unique indexes settle a race between two starts: one active job per person, one job per key.
            if (!await db.TrySaveUniqueAsync(ct)) ThrowError("An import is already running for you.", StatusCodes.Status409Conflict);
            File.Move(temp, ImportFiles.PathFor(settings, job.Id));
            await Send.ResponseAsync(ImportJobRead.From(job), StatusCodes.Status202Accepted, ct);
        }
        finally
        {
            if (File.Exists(temp)) ImportFiles.TryDelete(temp);
        }
    }
}
