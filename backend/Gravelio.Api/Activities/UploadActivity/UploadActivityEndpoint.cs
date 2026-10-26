using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
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
        Options(o => o.WithMetadata(UploadLimits.For(MaxUploadBytes)));
    }

    public override async Task HandleAsync(UploadActivityRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var user = (await users.GetUserAsync(User))!;
        var ext = Path.GetExtension(req.File.FileName).ToLowerInvariant();
        List<Activity> built = []; // ThrowError never returns, but the compiler can't see that
        try
        {
            await using var stream = req.File.OpenReadStream();
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

        // Re-uploading the same recording is rejected rather than creating a duplicate.
        var starts = built.Select(a => a.StartTime).ToList();
        var existing = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime)).Select(a => a.StartTime).ToListAsync(ct);
        var fresh = built.Where(a => !existing.Contains(a.StartTime)).ToList();
        if (fresh.Count == 0) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
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
            if (defaults.TryGetValue(a.ActivityType, out var gearId)) a.GearId = gearId;
        }
        db.Activities.AddRange(fresh);
        // A parallel upload of the same recording can win the race the check above can't prevent.
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
        var owner = PersonRead.From(user);
        await Send.OkAsync(fresh.Select(a => ActivityRead.From(a, owner)).ToList(), ct);
    }
}
