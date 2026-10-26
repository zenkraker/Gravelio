using Gravelio.Api.Common;
using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.ApiKeys;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Notifications;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class UploadActivityEndpoint(AppDb db, UserManager<User> users, TimeProvider time, ILogger<UploadActivityEndpoint> log)
    : Endpoint<UploadActivityRequest, List<ActivityRead>>
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    public override void Configure()
    {
        Post("/activities/upload");
        AllowFileUploads();
        // Signed in with the browser session, or with an API key from a sync tool.
        AuthSchemes(IdentityConstants.ApplicationScheme, ApiKeyAuth.Scheme);
        Options(o => o.WithMetadata([.. UploadLimits.For(MaxUploadBytes), new AllowsApiKey()]).RequireRateLimiting(RateLimiting.Upload));
    }

    public override async Task HandleAsync(UploadActivityRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var user = (await users.GetUserAsync(User))!;
        var ext = Path.GetExtension(req.File.FileName).ToLowerInvariant();
        List<Activity> built = []; // ThrowError never returns, but the compiler can't see that
        var fileHash = "";
        try
        {
            await using var stream = req.File.OpenReadStream();
            fileHash = await ActivityDuplicates.HashAsync(stream, ct);
            built = ActivityFileParser.Parse(stream, ext).Where(p => p.Points.Count > 0 || p.Summary is not null)
                .Select(p => ActivityBuilder.Build(p, userId, user.Timezone)).ToList();
        }
        catch (InvalidActivityFileException e)
        {
            ThrowError(e.Message, StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Failed to parse uploaded {Extension} file for user {UserId}", ext, userId);
            ThrowError("Failed to parse activity file", StatusCodes.Status422UnprocessableEntity);
        }

        // Uploading the same file again is refused: nothing new would be stored.
        foreach (var a in built) a.DedupKey = ActivityDuplicates.Key(fileHash, a.StartTime);
        var keys = built.Select(a => a.DedupKey!).ToList();
        var uploaded = await db.Activities.Where(a => a.UserId == userId && keys.Contains(a.DedupKey!)).Select(a => a.DedupKey!).ToListAsync(ct);
        var fresh = built.Where(a => !uploaded.Contains(a.DedupKey!)).ToList();
        if (fresh.Count == 0) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
        // A different file that starts at the same moment is most likely the same workout from a second device or app. It is stored, so
        // nothing is lost, but hidden, and the owner is told; they can unhide it if it is not a duplicate.
        var starts = fresh.Select(a => a.StartTime).ToList();
        var sameStart = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime)).Select(a => a.StartTime).ToListAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        // New activities start with the owner's default visibility; no privacy row means the safe private default.
        var defaultVisibility = (await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct))
            ?.DefaultActivityVisibility ?? Visibilities.Private;
        // Pre-select the user's default gear for each activity type (retired gear is skipped).
        var defaults = await db.DefaultGears.Where(d => d.UserId == userId && d.Gear.Active).ToDictionaryAsync(d => d.ActivityType, d => d.GearId, ct);
        foreach (var a in fresh)
        {
            a.CreatedAt = now;
            a.Visibility = defaultVisibility;
            a.IsHidden = sameStart.Contains(a.StartTime);
            if (defaults.TryGetValue(a.ActivityType, out var gearId)) a.GearId = gearId;
        }
        db.Activities.AddRange(fresh);
        // A parallel upload of the same recording can win the race the check above can't prevent.
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
        var duplicates = fresh.Where(a => a.IsHidden).ToList();
        foreach (var a in duplicates) NotificationWriter.AddDuplicate(db, userId, user.Name, a.Id, a.Name, now);
        if (duplicates.Count > 0) await NotificationWriter.TrySaveAsync(db, log, ct);
        var owner = PersonRead.From(user);
        await Send.OkAsync(fresh.Select(a => ActivityRead.From(a, owner)).ToList(), ct);
    }
}
