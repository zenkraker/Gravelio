using Gravelio.Api.Common;
using System.IO.Compression;
using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Gears;
using Gravelio.Api.Goals;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.DataTransfer;

/// Restores an export into the signed-in account. The file is untrusted: the zip is bounded by what is really read (not by sizes it
/// declares), nothing in it names a path, every value goes through the database's own constraints, and the whole import is one
/// transaction, so a bad file changes nothing. It never touches the profile, privacy settings, email or password, and it files
/// activities under the account's default visibility, because sharing is a decision for the person importing.
public sealed class ImportDataEndpoint(AppDb db, ILogger<ImportDataEndpoint> log) : Endpoint<ImportDataRequest, ImportDataResponse>
{
    public const long MaxUploadBytes = 500L * 1024 * 1024;

    const int MaxEntries = 100_002; // manifest, data, and up to 100,000 activities
    const long MaxEntryBytes = 200L * 1024 * 1024;
    const long MaxTotalBytes = 2L * 1024 * 1024 * 1024;
    const int BatchSize = 200;
    const int MaxLaps = 5_000;

    sealed class Rejected(string message) : Exception(message);

    public override void Configure()
    {
        Post("/users/me/import");
        AllowFileUploads();
        Options(o =>
        {
            o.WithMetadata(UploadLimits.For(MaxUploadBytes));
            o.RequireRateLimiting(RateLimiting.Sensitive);
        });
    }

    public override async Task HandleAsync(ImportDataRequest req, CancellationToken ct)
    {
        // ZipArchive needs a seekable stream, and a large export must not be buffered in memory.
        var tempPath = Path.Combine(Path.GetTempPath(), $"gravelio-import-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var file = File.Create(tempPath)) await req.File.CopyToAsync(file, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            ImportDataResponse result;
            try
            {
                using var archive = ZipFile.OpenRead(tempPath);
                result = await ImportAsync(archive, User.UserId(), ct);
            }
            catch (Exception e) when (e is Rejected or InvalidDataException or JsonException or DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var reason = e is Rejected ? e.Message : "The file is not a valid Gravelio export.";
                log.LogWarning(e, "Rejected a data import for user {UserId}", User.UserId());
                ThrowError(reason, StatusCodes.Status422UnprocessableEntity);
                return;
            }
            await transaction.CommitAsync(ct);
            await Send.OkAsync(result, ct);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    async Task<ImportDataResponse> ImportAsync(ZipArchive archive, int userId, CancellationToken ct)
    {
        if (archive.Entries.Count > MaxEntries) throw new Rejected("The export has too many files.");
        var budget = new long[] { MaxTotalBytes };

        var manifest = Parse<ExportManifest>(archive, "manifest.json", budget);
        if (manifest.Format != ExportJson.Format || manifest.Version != ExportJson.Version) throw new Rejected("This is not a Gravelio export this version can read.");
        var data = Parse<ExportedData>(archive, "data.json", budget);

        var gearMap = await ImportGearAsync(data, userId, ct);
        var goals = await ImportGoalsAsync(data.Goals, userId, ct);
        var health = await ImportHealthAsync(data, userId, ct);

        var defaultVisibility = (await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))
            ?.DefaultActivityVisibility ?? Visibilities.Private;
        var haveKeys = (await db.Activities.AsNoTracking().Where(a => a.UserId == userId && a.DedupKey != null).Select(a => a.DedupKey!).ToListAsync(ct)).ToHashSet();
        var haveStarts = (await db.Activities.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.StartTime).ToListAsync(ct)).ToHashSet();

        int imported = 0, skipped = 0, pending = 0;
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("activities/", StringComparison.Ordinal) && e.FullName.EndsWith(".json", StringComparison.Ordinal)))
        {
            var activity = Parse<Activity>(entry, budget);
            var key = activity.DedupKey ?? ActivityDuplicates.Key("export", activity.StartTime);
            if (haveKeys.Contains(key) || haveStarts.Contains(activity.StartTime))
            {
                skipped++;
                continue;
            }
            if (!ActivityTypes.Names.ContainsKey(activity.ActivityType)) throw new Rejected($"{entry.FullName} has an unknown activity type.");
            foreach (var stream in activity.Streams) CheckStream(stream, entry.FullName);
            if (activity.Laps.Count > MaxLaps || activity.Laps.Any(l => l.TotalElapsedTime < 0 || l.Distance < 0))
                throw new Rejected($"{entry.FullName} has invalid laps.");

            haveKeys.Add(key);
            haveStarts.Add(activity.StartTime);
            activity.Id = 0;
            activity.UserId = userId;
            activity.DedupKey = key;
            activity.Visibility = defaultVisibility;
            activity.GearId = activity.GearId is { } old && gearMap.TryGetValue(old, out var mapped) ? mapped : null;
            foreach (var stream in activity.Streams) stream.Id = 0;
            foreach (var lap in activity.Laps) lap.Id = 0;
            db.Activities.Add(activity);
            imported++;
            if (++pending >= BatchSize)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear(); // a long history must not pile up in the change tracker
                pending = 0;
            }
        }
        await db.SaveChangesAsync(ct);
        return new ImportDataResponse(gearMap.NewCount, goals, health, imported, skipped);
    }

    sealed class GearMap : Dictionary<int, int>
    {
        public int NewCount { get; set; }
    }

    async Task<GearMap> ImportGearAsync(ExportedData data, int userId, CancellationToken ct)
    {
        var map = new GearMap();
        var have = await db.Gears.Where(g => g.UserId == userId).ToListAsync(ct);
        var added = new List<(int Old, Gear Gear)>();
        foreach (var gear in data.Gears)
        {
            if (!GearTypes.IsValid(gear.GearType)) throw new Rejected($"Gear “{gear.Nickname}” has an unknown type.");
            var old = gear.Id;
            if (have.FirstOrDefault(g => g.Nickname == gear.Nickname && g.GearType == gear.GearType) is { } same)
            {
                map[old] = same.Id; // the same bike already exists here: reuse it and keep its components as they are
                continue;
            }
            foreach (var c in gear.Components)
            {
                if (!GearTypes.AllowsComponent(gear.GearType, c.Type)) throw new Rejected($"Gear “{gear.Nickname}” has a component its type does not allow.");
                c.Id = 0;
                c.GearId = 0;
            }
            gear.Id = 0;
            gear.UserId = userId;
            db.Gears.Add(gear);
            added.Add((old, gear));
        }
        await db.SaveChangesAsync(ct);
        foreach (var (old, gear) in added) map[old] = gear.Id;
        map.NewCount = added.Count;

        var slots = (await db.DefaultGears.Where(d => d.UserId == userId).Select(d => d.ActivityType).ToListAsync(ct)).ToHashSet();
        var gearTypes = await db.Gears.AsNoTracking().Where(g => g.UserId == userId).ToDictionaryAsync(g => g.Id, g => new { g.GearType, g.Active }, ct);
        foreach (var d in data.DefaultGears)
        {
            if (slots.Contains(d.ActivityType) || !GearTypes.DefaultSlots.TryGetValue(d.ActivityType, out var slotType)) continue;
            if (!map.TryGetValue(d.GearId, out var gearId) || gearTypes[gearId] is not { Active: true } g || g.GearType != slotType) continue;
            slots.Add(d.ActivityType);
            db.DefaultGears.Add(new DefaultGear { UserId = userId, ActivityType = d.ActivityType, GearId = gearId });
        }
        await db.SaveChangesAsync(ct);
        return map;
    }

    async Task<int> ImportGoalsAsync(List<Goal> goals, int userId, CancellationToken ct)
    {
        var have = (await db.Goals.AsNoTracking().Where(g => g.UserId == userId).Select(g => new { g.Interval, g.ActivityGroup, g.Metric }).ToListAsync(ct))
            .Select(g => (g.Interval, g.ActivityGroup, g.Metric)).ToHashSet();
        var count = 0;
        foreach (var goal in goals)
        {
            if (!GoalRules.Intervals.Contains(goal.Interval) || !GoalRules.Groups.ContainsKey(goal.ActivityGroup) || !GoalRules.Metrics.Contains(goal.Metric)
                || goal.Target is < 1 or > GoalRules.MaxTarget) throw new Rejected("A goal in the file is not valid.");
            if (!have.Add((goal.Interval, goal.ActivityGroup, goal.Metric))) continue;
            goal.Id = 0;
            goal.UserId = userId;
            db.Goals.Add(goal);
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }

    async Task<int> ImportHealthAsync(ExportedData data, int userId, CancellationToken ct)
    {
        var count = 0;
        var weight = (await db.HealthWeights.Where(h => h.UserId == userId).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Weight.Where(h => weight.Add(h.Date))) { h.Id = 0; h.UserId = userId; db.HealthWeights.Add(h); count++; }
        var steps = (await db.HealthSteps.Where(h => h.UserId == userId).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Steps.Where(h => steps.Add(h.Date))) { h.Id = 0; h.UserId = userId; db.HealthSteps.Add(h); count++; }
        var sleep = (await db.HealthSleeps.Where(h => h.UserId == userId).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Sleep.Where(h => sleep.Add(h.Date))) { h.Id = 0; h.UserId = userId; db.HealthSleeps.Add(h); count++; }
        var water = (await db.HealthWaters.Where(h => h.UserId == userId).Select(h => h.Date).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Water.Where(h => water.Add(h.Date))) { h.Id = 0; h.UserId = userId; db.HealthWaters.Add(h); count++; }
        var fasts = (await db.HealthFastings.Where(h => h.UserId == userId).Select(h => h.FastStartTime).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Fasting.Where(h => fasts.Add(h.FastStartTime)))
        {
            h.Id = 0;
            h.UserId = userId;
            // A fast that was still running when it was exported is not running here: only one fast may be in progress at a time.
            if (h.Status == FastingStatuses.InProgress) h.Status = FastingStatuses.Cancelled;
            db.HealthFastings.Add(h);
            count++;
        }
        var poops = (await db.HealthPoops.Where(h => h.UserId == userId).Select(h => h.DateTime).ToListAsync(ct)).ToHashSet();
        foreach (var h in data.Poop.Where(h => poops.Add(h.DateTime))) { h.Id = 0; h.UserId = userId; db.HealthPoops.Add(h); count++; }
        if (data.Targets is { } targets && !await db.HealthTargets.AnyAsync(t => t.UserId == userId, ct))
        {
            targets.Id = 0;
            targets.UserId = userId;
            db.HealthTargets.Add(targets);
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }

    static void CheckStream(ActivityStream stream, string file)
    {
        if (stream.StreamType is < StreamTypes.Hr or > StreamTypes.Temperature) throw new Rejected($"{file} has an unknown stream type.");
        using var doc = JsonDocument.Parse(stream.Waypoints, new JsonDocumentOptions { MaxDepth = 4 });
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() > 500_000) throw new Rejected($"{file} has a stream that is not valid.");
    }

    static T Parse<T>(ZipArchive archive, string name, long[] budget) =>
        Parse<T>(archive.GetEntry(name) ?? throw new Rejected($"The file has no {name}."), budget);

    /// Reads at most <see cref="MaxEntryBytes"/> from one entry and counts it against the total, whatever the zip claims about its size.
    static T Parse<T>(ZipArchiveEntry entry, long[] budget)
    {
        using var input = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        long read = 0;
        int n;
        while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            read += n;
            budget[0] -= n;
            if (read > MaxEntryBytes || budget[0] < 0) throw new Rejected("The export is larger than this server accepts when unpacked.");
            buffer.Write(chunk, 0, n);
        }
        return JsonSerializer.Deserialize<T>(buffer.ToArray(), ExportJson.Options) ?? throw new Rejected($"{entry.FullName} is empty.");
    }
}
