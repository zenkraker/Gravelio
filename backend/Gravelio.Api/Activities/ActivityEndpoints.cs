using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public record ActivityRead(int Id, string? Name, string? Description, int Distance, int ActivityType, DateTime StartTime, DateTime EndTime,
    string? Timezone, double TotalElapsedTime, double TotalTimerTime, int? ElevationGain, int? ElevationLoss, double? Pace,
    double? AverageSpeed, double? MaxSpeed, int? AveragePower, int? MaxPower, int? NormalizedPower, int? AverageHr, int? MaxHr,
    int? AverageCad, int? MaxCad, int? Calories, int Visibility, string? TrackerManufacturer, string? TrackerModel, DateTime CreatedAt)
{
    public static ActivityRead From(Activity a) => new(a.Id, a.Name, a.Description, a.Distance, a.ActivityType, a.StartTime, a.EndTime,
        a.Timezone, a.TotalElapsedTime, a.TotalTimerTime, a.ElevationGain, a.ElevationLoss, a.Pace, a.AverageSpeed, a.MaxSpeed,
        a.AveragePower, a.MaxPower, a.NormalizedPower, a.AverageHr, a.MaxHr, a.AverageCad, a.MaxCad, a.Calories, a.Visibility,
        a.TrackerManufacturer, a.TrackerModel, a.CreatedAt);
}

public record ActivityUpdate(
    [Required, MaxLength(250)] string Name,
    [MaxLength(2500)] string? Description,
    [Range(1, 47)] int ActivityType,
    [Range(Visibilities.Public, Visibilities.Private)] int Visibility);

public record ActivityPage(List<ActivityRead> Items, int Total, int Page, int PageSize);
public record StreamRead(int StreamType, JsonElement Waypoints);

public static class ActivityEndpoints
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    public static void MapActivities(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/activities").RequireAuthorization();

        g.MapGet("/types", () => ActivityTypes.Names);

        g.MapGet("", async (HttpContext http, AppDb db, int page = 1, int pageSize = 20) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = db.Activities.Where(a => a.UserId == http.User.UserId());
            var items = await query.OrderByDescending(a => a.StartTime).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new ActivityPage(items.Select(ActivityRead.From).ToList(), await query.CountAsync(), page, pageSize);
        });

        g.MapGet("/{id:int}", async (int id, HttpContext http, AppDb db) =>
            await db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == http.User.UserId()) is { } a
                ? Results.Ok(ActivityRead.From(a)) : Results.NotFound());

        g.MapGet("/{id:int}/streams", async (int id, HttpContext http, AppDb db) =>
        {
            if (!await db.Activities.AnyAsync(a => a.Id == id && a.UserId == http.User.UserId())) return Results.NotFound();
            var streams = await db.ActivityStreams.Where(s => s.ActivityId == id).OrderBy(s => s.StreamType).ToListAsync();
            return Results.Ok(streams.Select(s => new StreamRead(s.StreamType, JsonDocument.Parse(s.Waypoints).RootElement)));
        });

        g.MapPut("/{id:int}", async (int id, ActivityUpdate req, HttpContext http, AppDb db) =>
        {
            var a = await db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == http.User.UserId());
            if (a is null) return Results.NotFound();
            (a.Name, a.Description, a.ActivityType, a.Visibility) = (req.Name, req.Description, req.ActivityType, req.Visibility);
            await db.SaveChangesAsync();
            return Results.Ok(ActivityRead.From(a));
        });

        g.MapDelete("/{id:int}", async (int id, HttpContext http, AppDb db) =>
            await db.Activities.Where(a => a.Id == id && a.UserId == http.User.UserId()).ExecuteDeleteAsync() > 0
                ? Results.NoContent() : Results.NotFound());

        g.MapPost("/upload", async (IFormFile file, HttpContext http, AppDb db, ILogger<Activity> log) =>
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ActivityFileParser.SupportedExtensions.Contains(ext))
                return Results.Problem($"Unsupported file type. Allowed: {string.Join(", ", ActivityFileParser.SupportedExtensions)}", statusCode: 400);
            if (file.Length is 0 or > MaxUploadBytes) return Results.Problem("File is empty or larger than 50 MB", statusCode: 400);

            var userId = http.User.UserId();
            var user = (await db.Users.FindAsync(userId))!;
            List<Activity> built;
            try
            {
                await using var stream = file.OpenReadStream();
                built = ActivityFileParser.Parse(stream, ext).Where(p => p.Points.Count > 0 || p.Summary is not null)
                    .Select(p => ActivityBuilder.Build(p, userId, user.Timezone)).ToList();
            }
            catch (InvalidActivityFileException e)
            {
                return Results.Problem(e.Message, statusCode: 422);
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Failed to parse uploaded {Extension} file for user {UserId}", ext, userId);
                return Results.Problem("Failed to parse activity file", statusCode: 422);
            }

            // Re-uploading the same recording is rejected rather than creating a duplicate.
            var starts = built.Select(a => a.StartTime).ToList();
            var existing = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime)).Select(a => a.StartTime).ToListAsync();
            var fresh = built.Where(a => !existing.Contains(a.StartTime)).ToList();
            if (fresh.Count == 0) return Results.Problem("This activity has already been uploaded", statusCode: 409);
            db.Activities.AddRange(fresh);
            await db.SaveChangesAsync();
            return Results.Ok(fresh.Select(ActivityRead.From));
        }).DisableAntiforgery(); // bearer-token auth only, so there is no ambient credential to forge
    }
}
