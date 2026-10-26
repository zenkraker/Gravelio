using Gravelio.Api.Activities.Import;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public enum IngestOutcome
{
    /// New activities were stored.
    Created,

    /// Every activity in the file was already uploaded; nothing was stored.
    Duplicate,

    /// The file could not be read as an activity file.
    Invalid,
}

public sealed record IngestResult(IngestOutcome Outcome, List<Activity> Activities, string? Error);

/// What one uploaded activity file turns into, shared by the single upload and the bulk import so both treat a file the same way: the
/// same file again is refused, a different file starting at the same moment is stored hidden (see <see cref="Activity.DedupKey"/>), and
/// new activities start with the owner's default visibility and default gear.
public static class ActivityIngestion
{
    /// <paramref name="file"/> must be seekable: it is read once to hash it and again to parse it.
    public static async Task<IngestResult> IngestAsync(AppDb db, User user, string extension, Stream file, int defaultVisibility,
        IReadOnlyDictionary<int, int> defaultGears, DateTime now, ILogger log, CancellationToken ct)
    {
        var userId = user.Id;
        string fileHash;
        List<Activity> built;
        try
        {
            fileHash = await ActivityDuplicates.HashAsync(file, ct);
            built = ActivityFileParser.Parse(file, extension).Where(p => p.Points.Count > 0 || p.Summary is not null)
                .Select(p => ActivityBuilder.Build(p, userId, user.Timezone)).ToList();
        }
        catch (InvalidActivityFileException e)
        {
            return new(IngestOutcome.Invalid, [], e.Message);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Failed to parse {Extension} file for user {UserId}", extension, userId);
            return new(IngestOutcome.Invalid, [], "Failed to parse activity file");
        }

        foreach (var a in built) a.DedupKey = ActivityDuplicates.Key(fileHash, a.StartTime);
        var keys = built.Select(a => a.DedupKey!).ToList();
        var uploaded = await db.Activities.Where(a => a.UserId == userId && keys.Contains(a.DedupKey!)).Select(a => a.DedupKey!).ToListAsync(ct);
        var fresh = built.Where(a => !uploaded.Contains(a.DedupKey!)).ToList();
        if (fresh.Count == 0) return new(IngestOutcome.Duplicate, [], null);

        // A different file that starts at the same moment is most likely the same workout from a second device or app. It is stored, so
        // nothing is lost, but hidden, and the owner is told; they can unhide it if it is not a duplicate.
        var starts = fresh.Select(a => a.StartTime).ToList();
        var sameStart = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime)).Select(a => a.StartTime).ToListAsync(ct);
        foreach (var a in fresh)
        {
            a.CreatedAt = now;
            a.Visibility = defaultVisibility;
            a.IsHidden = sameStart.Contains(a.StartTime);
            if (defaultGears.TryGetValue(a.ActivityType, out var gearId)) a.GearId = gearId;
        }
        db.Activities.AddRange(fresh);
        // A parallel upload of the same recording can win the race the check above can't prevent.
        if (!await db.TrySaveUniqueAsync(ct))
        {
            db.ChangeTracker.Clear();
            return new(IngestOutcome.Duplicate, [], null);
        }
        return new(IngestOutcome.Created, fresh, null);
    }

    /// The owner's default visibility (private when they never set one) and their default gear by activity type; retired gear is skipped.
    public static async Task<(int Visibility, Dictionary<int, int> Gears)> DefaultsAsync(AppDb db, int userId, CancellationToken ct) =>
    (
        (await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))?.DefaultActivityVisibility ?? Visibilities.Private,
        await db.DefaultGears.AsNoTracking().Where(d => d.UserId == userId && d.Gear.Active).ToDictionaryAsync(d => d.ActivityType, d => d.GearId, ct)
    );
}
