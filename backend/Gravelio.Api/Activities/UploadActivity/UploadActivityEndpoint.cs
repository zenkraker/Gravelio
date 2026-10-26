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
        var now = time.GetUtcNow().UtcDateTime;
        var (defaultVisibility, defaultGears) = await ActivityIngestion.DefaultsAsync(db, userId, ct);
        await using var stream = req.File.OpenReadStream();
        var result = await ActivityIngestion.IngestAsync(db, user, ext, stream, defaultVisibility, defaultGears, now, log, ct);
        if (result.Outcome == IngestOutcome.Invalid) ThrowError(result.Error!, StatusCodes.Status422UnprocessableEntity);
        // Uploading the same file again is refused: nothing new would be stored.
        if (result.Outcome == IngestOutcome.Duplicate) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
        var fresh = result.Activities;
        var duplicates = fresh.Where(a => a.IsHidden).ToList();
        foreach (var a in duplicates) NotificationWriter.AddDuplicate(db, userId, user.Name, a.Id, a.Name, now);
        // Followers hear about an activity they are allowed to see; a hidden duplicate is not one of those.
        var shared = fresh.Where(a => !a.IsHidden && a.Visibility != Visibilities.Private).ToList();
        if (shared.Count > 0)
        {
            var followers = await db.Follows.Where(f => f.FolloweeId == userId && f.Status == FollowStatuses.Accepted).Select(f => f.FollowerId).ToListAsync(ct);
            foreach (var a in shared)
                foreach (var follower in followers) db.AddNewActivity(follower, userId, user.Name, a.Id, a.Name, now);
        }
        if (duplicates.Count > 0 || shared.Count > 0) await NotificationWriter.TrySaveAsync(db, log, ct);
        var owner = PersonRead.From(user);
        await Send.OkAsync(fresh.Select(a => ActivityRead.From(a, owner)).ToList(), ct);
    }
}
