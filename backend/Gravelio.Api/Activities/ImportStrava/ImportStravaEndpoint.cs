using Gravelio.Api.Common;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ImportStravaEndpoint(AppDb db, UserManager<User> users, TimeProvider time, ILogger<ImportStravaEndpoint> log)
    : Endpoint<ImportStravaRequest, ImportStravaResponse>
{
    /// A full Strava export can be large; keep it separate from the 50 MB single-activity upload.
    public const long MaxUploadBytes = 500 * 1024 * 1024;

    public override void Configure()
    {
        Post("/activities/import/strava");
        AllowFileUploads();
        Options(o => o.WithMetadata(UploadLimits.For(MaxUploadBytes)).RequireRateLimiting(RateLimiting.Upload));
    }

    public override async Task HandleAsync(ImportStravaRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var user = (await users.GetUserAsync(User))!;
        // ZipArchive needs a seekable stream, and a full export must not be buffered in memory.
        var tempPath = Path.Combine(Path.GetTempPath(), $"gravelio-strava-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var file = File.Create(tempPath)) await req.File.CopyToAsync(file, ct);

            StravaExport.Result export;
            await using (var stream = File.OpenRead(tempPath))
            {
                try
                {
                    export = StravaExport.Read(stream);
                }
                catch (InvalidActivityFileException e)
                {
                    ThrowError(e.Message, StatusCodes.Status422UnprocessableEntity);
                    return;
                }
                catch (Exception e)
                {
                    log.LogWarning(e, "Failed to read a Strava export for user {UserId}", userId);
                    ThrowError("Failed to read the Strava export", StatusCodes.Status422UnprocessableEntity);
                    return;
                }
            }

            var built = new List<(Activity Activity, string? Gear)>();
            var errors = new List<string>(export.Errors);
            foreach (var item in export.Activities)
            {
                try
                {
                    built.Add((ActivityBuilder.Build(item.Parsed, userId, user.Timezone), item.Gear));
                }
                catch (InvalidActivityFileException e)
                {
                    errors.Add($"{item.File}: {e.Message}");
                }
            }
            // Nothing usable in the zip: answer with the reason instead of a silent zero-activity import.
            if (built.Count == 0 && errors.Count > 0) ThrowError(errors[0], StatusCodes.Status422UnprocessableEntity);

            var now = time.GetUtcNow().UtcDateTime;
            // An activity's own Strava gear wins over the user's default gear for that activity type.
            var gearIds = await EnsureGearAsync(export.Gear, userId, now, ct);
            foreach (var (activity, gear) in built)
                if (gear is not null && gearIds.TryGetValue(gear, out var gearId)) activity.GearId = gearId;

            // Re-importing an export (or overlapping exports) skips what is already there instead of failing the batch.
            var builtActivities = built.Select(b => b.Activity).ToList();
            var starts = builtActivities.Select(a => a.StartTime).ToList();
            var existing = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime))
                .Select(a => a.StartTime).ToListAsync(ct);
            var fresh = builtActivities.Where(a => !existing.Contains(a.StartTime)).DistinctBy(a => a.StartTime).ToList();

            var defaultVisibility = (await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))
                ?.DefaultActivityVisibility ?? Visibilities.Private;
            var defaults = await db.DefaultGears.Where(d => d.UserId == userId && d.Gear.Active).ToDictionaryAsync(d => d.ActivityType, d => d.GearId, ct);
            foreach (var a in fresh)
            {
                a.CreatedAt = now;
                a.Visibility = defaultVisibility;
                a.DedupKey = ActivityDuplicates.Key("strava", a.StartTime); // one imported activity per start, even if two imports run at once
                if (a.GearId is null && defaults.TryGetValue(a.ActivityType, out var gearId)) a.GearId = gearId;
            }
            if (fresh.Count > 0)
            {
                db.Activities.AddRange(fresh);
                if (!await db.TrySaveUniqueAsync(ct)) ThrowError("Some of these activities were already imported", StatusCodes.Status409Conflict);
                log.LogInformation("Imported {Count} activities from a Strava export for user {UserId}", fresh.Count, userId);
            }

            await Send.OkAsync(new ImportStravaResponse(fresh.Count, built.Count - fresh.Count, errors.Count, errors.Take(20).ToList()), ct);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// Creates the bikes and shoes from the export that the user does not have yet, keyed by nickname.
    async Task<Dictionary<string, int>> EnsureGearAsync(List<StravaExport.StravaGear> gear, int userId, DateTime now, CancellationToken ct)
    {
        var existing = await db.Gears.AsNoTracking().Where(g => g.UserId == userId)
            .Select(g => new { g.Nickname, g.Id }).ToListAsync(ct);
        // Nicknames are not unique in the database, so keep the first match instead of throwing.
        // shortcut: two parallel imports of the same export can create the gear twice; add a unique index if that matters.
        var ids = existing.GroupBy(g => g.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var added = gear.Where(g => !ids.ContainsKey(g.Nickname))
            .DistinctBy(g => g.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Gear
            {
                UserId = userId,
                Nickname = g.Nickname,
                Brand = g.Brand,
                Model = g.Model,
                GearType = g.Type,
                CreatedAt = now,
            })
            .ToList();
        if (added.Count == 0) return ids;
        db.Gears.AddRange(added);
        await db.SaveChangesAsync(ct);
        foreach (var g in added) ids[g.Nickname] = g.Id;
        log.LogInformation("Imported {Count} gear items from a Strava export for user {UserId}", added.Count, userId);
        return ids;
    }
}
