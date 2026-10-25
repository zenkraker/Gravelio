using System.Linq.Expressions;
using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed record ActivityRead(int Id, string? Name, string? Description, int Distance, int ActivityType, DateTime StartTime, DateTime EndTime,
    string? Timezone, double TotalElapsedTime, double TotalTimerTime, int? ElevationGain, int? ElevationLoss, double? Pace,
    double? AverageSpeed, double? MaxSpeed, int? AveragePower, int? MaxPower, int? NormalizedPower, int? AverageHr, int? MaxHr,
    int? AverageCad, int? MaxCad, int? Calories, int Visibility, string? TrackerManufacturer, string? TrackerModel, DateTime CreatedAt, int? GearId)
{
    // An expression so EF Core can translate it and select only these columns.
    public static readonly Expression<Func<Activity, ActivityRead>> Projection = a => new ActivityRead(a.Id, a.Name, a.Description, a.Distance,
        a.ActivityType, a.StartTime, a.EndTime, a.Timezone, a.TotalElapsedTime, a.TotalTimerTime, a.ElevationGain, a.ElevationLoss, a.Pace,
        a.AverageSpeed, a.MaxSpeed, a.AveragePower, a.MaxPower, a.NormalizedPower, a.AverageHr, a.MaxHr, a.AverageCad, a.MaxCad, a.Calories,
        a.Visibility, a.TrackerManufacturer, a.TrackerModel, a.CreatedAt, a.GearId);

    static readonly Func<Activity, ActivityRead> Compiled = Projection.Compile();

    public static ActivityRead From(Activity a) => Compiled(a);
}

public sealed class ActivityTypesEndpoint : EndpointWithoutRequest<IReadOnlyDictionary<int, string>>
{
    public override void Configure() => Get("/activities/types");

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(ActivityTypes.Names, ct);
}

public sealed class ListActivitiesRequest : PagedRequest;

public sealed class ListActivitiesValidator : PagedValidator<ListActivitiesRequest>;

public sealed class ListActivitiesEndpoint(AppDb db) : Endpoint<ListActivitiesRequest, PagedResult<ActivityRead>>
{
    public override void Configure() => Get("/activities");

    public override async Task HandleAsync(ListActivitiesRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.Activities.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.StartTime)
            .ToPageAsync(req, page => page.Select(ActivityRead.Projection), ct), ct);
    }
}

public sealed class GetActivityEndpoint(AppDb db) : Endpoint<IdRequest, ActivityRead>
{
    public override void Configure() => Get("/activities/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var activity = await db.Activities.AsNoTracking().OwnedBy(User.UserId(), req.Id).Select(ActivityRead.Projection).FirstOrDefaultAsync(ct);
        if (activity is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(activity, ct);
    }
}

public sealed record StreamRead(int StreamType, JsonElement Waypoints);

public sealed class GetStreamsEndpoint(AppDb db) : Endpoint<IdRequest, List<StreamRead>>
{
    // Clone lets the JsonDocument (and its pooled buffers) be released right away.
    static JsonElement ParseWaypoints(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public override void Configure() => Get("/activities/{id:int}/streams");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (!await db.Activities.OwnedBy(User.UserId(), req.Id).AnyAsync(ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var streams = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == req.Id).OrderBy(s => s.StreamType).ToListAsync(ct);
        await Send.OkAsync(streams.Select(s => new StreamRead(s.StreamType, ParseWaypoints(s.Waypoints))).ToList(), ct);
    }
}

public sealed record UpdateActivityRequest(int Id, string Name, string? Description, int ActivityType, int Visibility);

public sealed class UpdateActivityValidator : Validator<UpdateActivityRequest>
{
    public UpdateActivityValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2500);
        RuleFor(x => x.ActivityType).Must(ActivityTypes.Names.ContainsKey).WithMessage("Unknown activity type");
        RuleFor(x => x.Visibility).InclusiveBetween(Visibilities.Public, Visibilities.Private);
    }
}

public sealed class UpdateActivityEndpoint(AppDb db) : Endpoint<UpdateActivityRequest, ActivityRead>
{
    public override void Configure() => Put("/activities/{id:int}");

    public override async Task HandleAsync(UpdateActivityRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var activity = await db.Activities.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (activity.Name, activity.Description, activity.ActivityType, activity.Visibility) = (req.Name, req.Description, req.ActivityType, req.Visibility);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ActivityRead.From(activity), ct);
    }
}

/// <see cref="GearId"/> of null removes the gear from the activity.
public sealed record SetActivityGearRequest(int Id, int? GearId);

public sealed class SetActivityGearEndpoint(AppDb db) : Endpoint<SetActivityGearRequest, ActivityRead>
{
    public override void Configure() => Put("/activities/{id:int}/gear");

    public override async Task HandleAsync(SetActivityGearRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var activity = await db.Activities.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // Another user's gear must look exactly like gear that doesn't exist.
        if (req.GearId is { } gearId && !await db.Gears.AnyAsync(g => g.Id == gearId && g.UserId == userId, ct))
            ThrowError(r => r.GearId, "Gear not found", StatusCodes.Status404NotFound);
        activity.GearId = req.GearId;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ActivityRead.From(activity), ct);
    }
}

public sealed class DeleteActivityEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/activities/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (await db.Activities.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}

public sealed class UploadActivityRequest
{
    public required IFormFile File { get; init; }
}

public sealed class UploadActivityValidator : Validator<UploadActivityRequest>
{
    public UploadActivityValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage("Attach the activity file in the 'file' form field.");
        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length).InclusiveBetween(1, UploadActivityEndpoint.MaxUploadBytes).WithMessage("File is empty or larger than 50 MB");
            RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Must(ActivityFileParser.SupportedExtensions.Contains)
                .WithMessage($"Unsupported file type. Allowed: {string.Join(", ", ActivityFileParser.SupportedExtensions)}");
        });
    }
}

public sealed class UploadActivityEndpoint(AppDb db, UserManager<User> users, TimeProvider time, ILogger<UploadActivityEndpoint> log)
    : Endpoint<UploadActivityRequest, List<ActivityRead>>
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    public override void Configure()
    {
        Post("/activities/upload");
        AllowFileUploads();
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
        // Pre-select the user's default gear for each activity type (retired gear is skipped).
        var defaults = await db.DefaultGears.Where(d => d.UserId == userId && d.Gear.Active).ToDictionaryAsync(d => d.ActivityType, d => d.GearId, ct);
        foreach (var a in fresh)
        {
            a.CreatedAt = now;
            if (defaults.TryGetValue(a.ActivityType, out var gearId)) a.GearId = gearId;
        }
        db.Activities.AddRange(fresh);
        // A parallel upload of the same recording can win the race the check above can't prevent.
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("This activity has already been uploaded", StatusCodes.Status409Conflict);
        await Send.OkAsync(fresh.Select(ActivityRead.From).ToList(), ct);
    }
}
