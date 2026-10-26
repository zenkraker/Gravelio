using System.IO.Compression;
using System.Text.Json;
using Gravelio.Api.Activities;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Imports;

/// Runs queued bulk imports, oldest first. A job that stops being heard from (a crash) is queued again; that is safe because a file that
/// was already imported is skipped as a duplicate. One pass holds the advisory lock, so only one replica imports at a time.
public sealed class BulkImportJob(IServiceScopeFactory scopes, TimeProvider time, IOptions<ImportOptions> options, ILogger<BulkImportJob> logger)
    : PeriodicJob(scopes, time, logger)
{
    public const int Lock = 0x494D50; // "IMP"
    const int MaxFileBytes = 50 * 1024 * 1024; // the same limit as a single upload
    const int MaxRecordedFailures = 50;
    static readonly TimeSpan Stale = TimeSpan.FromMinutes(2);
    static readonly TimeSpan OrphanAge = TimeSpan.FromHours(1);

    protected override int LockKey => Lock;

    protected override TimeSpan Interval => TimeSpan.FromSeconds(10);

    protected override bool Enabled => options.Value.Enabled;

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDb>();
        var now = Time.GetUtcNow().UtcDateTime;
        await db.ImportJobs.Where(j => j.Status == ImportStatuses.Running && j.HeartbeatAt < now - Stale)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ImportStatuses.Queued), ct);
        SweepOrphans(await db.ImportJobs.AsNoTracking().Where(j => j.Status == ImportStatuses.Queued || j.Status == ImportStatuses.Running).Select(j => j.Id).ToListAsync(ct));

        while (!ct.IsCancellationRequested && await db.ImportJobs.Where(j => j.Status == ImportStatuses.Queued).OrderBy(j => j.Id).FirstOrDefaultAsync(ct) is { } job)
        {
            await RunJobAsync(db, job, ct);
            db.ChangeTracker.Clear();
        }
    }

    async Task RunJobAsync(AppDb db, ImportJob job, CancellationToken ct)
    {
        var path = ImportFiles.PathFor(options.Value, job.Id);
        (job.Status, job.StartedAt, job.HeartbeatAt) = (ImportStatuses.Running, Time.GetUtcNow().UtcDateTime, Time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        var failures = new List<ImportFailure>();
        string? error = null;
        try
        {
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == job.UserId, ct);
            var (visibility, gears) = await ActivityIngestion.DefaultsAsync(db, job.UserId, ct);
            using var archive = ZipFile.OpenRead(path);
            var budget = new ZipBudget(options.Value.MaxExpandedMegabytes * 1024 * 1024, MaxFileBytes);
            int processed = 0, imported = 0, skipped = 0, hidden = 0, failed = 0;
            foreach (var entry in archive.Entries.Where(ImportFiles.IsActivityFile))
            {
                if (await db.ImportJobs.AsNoTracking().Where(j => j.Id == job.Id).Select(j => j.Status).FirstAsync(ct) == ImportStatuses.Cancelled) break;
                try
                {
                    using var file = new MemoryStream();
                    await using (var source = budget.Open(entry)) await source.CopyToAsync(file, ct);
                    var result = await ActivityIngestion.IngestAsync(db, user, Path.GetExtension(entry.Name).ToLowerInvariant(), file, visibility, gears,
                        Time.GetUtcNow().UtcDateTime, logger, ct);
                    switch (result.Outcome)
                    {
                        case IngestOutcome.Created:
                            imported += result.Activities.Count(a => !a.IsHidden);
                            hidden += result.Activities.Count(a => a.IsHidden);
                            break;
                        case IngestOutcome.Duplicate:
                            skipped++;
                            break;
                        default:
                            failed++;
                            if (failures.Count < MaxRecordedFailures) failures.Add(new ImportFailure(entry.FullName, result.Error ?? "Could not be read"));
                            break;
                    }
                }
                catch (Exception e) when (e is InvalidActivityFileException or DbUpdateException or InvalidDataException or IOException)
                {
                    failed++;
                    if (failures.Count < MaxRecordedFailures) failures.Add(new ImportFailure(entry.FullName, e is DbUpdateException ? "Could not be stored" : e.Message));
                    db.ChangeTracker.Clear();
                }
                processed++;
                var (p, i, s, h, f, json, beat) = (processed, imported, skipped, hidden, failed, JsonSerializer.Serialize(failures, ImportJobRead.Json), Time.GetUtcNow().UtcDateTime);
                await db.ImportJobs.Where(j => j.Id == job.Id && j.Status == ImportStatuses.Running).ExecuteUpdateAsync(u => u
                    .SetProperty(j => j.ProcessedFiles, p).SetProperty(j => j.Imported, i).SetProperty(j => j.Skipped, s)
                    .SetProperty(j => j.HiddenDuplicates, h).SetProperty(j => j.Failed, f).SetProperty(j => j.Failures, json).SetProperty(j => j.HeartbeatAt, beat), ct);
                if (budget.Exhausted)
                {
                    error = "The zip expands to more data than this server accepts, so the import stopped.";
                    break;
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Bulk import {JobId} failed", job.Id);
            error = e is InvalidDataException ? "That is not a readable zip file." : "The import stopped unexpectedly.";
        }
        finally
        {
            ImportFiles.TryDelete(path);
        }

        var (status, finished) = (error is null ? ImportStatuses.Completed : ImportStatuses.Failed, Time.GetUtcNow().UtcDateTime);
        // A cancelled job keeps its status.
        await db.ImportJobs.Where(j => j.Id == job.Id && j.Status == ImportStatuses.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status).SetProperty(j => j.Error, error).SetProperty(j => j.FinishedAt, finished), ct);
        await db.ImportJobs.Where(j => j.Id == job.Id && j.Status == ImportStatuses.Cancelled && j.FinishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.FinishedAt, finished), ct);
    }

    /// Zips whose job is gone or finished (an upload that was never queued, a crash between steps) are removed once they are an hour old.
    void SweepOrphans(List<int> activeJobs)
    {
        var directory = options.Value.Directory;
        if (!Directory.Exists(directory)) return;
        var cutoff = Time.GetUtcNow().UtcDateTime - OrphanAge;
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (Path.GetExtension(file) == ".zip" && int.TryParse(name, out var id) && activeJobs.Contains(id)) continue;
            if (File.GetLastWriteTimeUtc(file) < cutoff) ImportFiles.TryDelete(file);
        }
    }
}
