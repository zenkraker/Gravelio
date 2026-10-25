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
    int? AverageCad, int? MaxCad, int? Calories, int Visibility, string? TrackerManufacturer, string? TrackerModel, DateTime CreatedAt)
{
    // An expression so EF Core can translate it and select only these columns.
    public static readonly Expression<Func<Activity, ActivityRead>> Projection = a => new ActivityRead(a.Id, a.Name, a.Description, a.Distance,
        a.ActivityType, a.StartTime, a.EndTime, a.Timezone, a.TotalElapsedTime, a.TotalTimerTime, a.ElevationGain, a.ElevationLoss, a.Pace,
        a.AverageSpeed, a.MaxSpeed, a.AveragePower, a.MaxPower, a.NormalizedPower, a.AverageHr, a.MaxHr, a.AverageCad, a.MaxCad, a.Calories,
        a.Visibility, a.TrackerManufacturer, a.TrackerModel, a.CreatedAt);

    public static ActivityRead From(Activity a) => Projection.Compile()(a);
}

public sealed record ActivityPage(List<ActivityRead> Items, int Total, int Page, int PageSize);

public sealed class ActivityTypesEndpoint : EndpointWithoutRequest<IReadOnlyDictionary<int, string>>
{
    public override void Configure() => Get("/activities/types");

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(ActivityTypes.Names, ct);
}

public sealed class ListActivitiesRequest
{
    public int Page { get; init; } = 1;

    [BindFrom("page_size")]
    public int PageSize { get; init; } = 20;
}

public sealed class ListActivitiesValidator : Validator<ListActivitiesRequest>
{
    public ListActivitiesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListActivitiesEndpoint(AppDb db) : Endpoint<ListActivitiesRequest, ActivityPage>
{
    public override void Configure() => Get("/activities");

    public override async Task HandleAsync(ListActivitiesRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var query = db.Activities.AsNoTracking().Where(a => a.UserId == userId);
        var items = await query.OrderByDescending(a => a.StartTime).Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)
            .Select(ActivityRead.Projection).ToListAsync(ct);
        await Send.OkAsync(new ActivityPage(items, await query.CountAsync(ct), req.Page, req.PageSize), ct);
    }
}

public sealed record ActivityIdRequest(int Id);

public sealed class GetActivityEndpoint(AppDb db) : Endpoint<ActivityIdRequest, ActivityRead>
{
    public override void Configure() => Get("/activities/{id:int}");

    public override async Task HandleAsync(ActivityIdRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var activity = await db.Activities.AsNoTracking().Where(a => a.Id == req.Id && a.UserId == userId)
            .Select(ActivityRead.Projection).FirstOrDefaultAsync(ct);
        if (activity is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(activity, ct);
    }
}

public sealed record StreamRead(int StreamType, JsonElement Waypoints);

public sealed class GetStreamsEndpoint(AppDb db) : Endpoint<ActivityIdRequest, List<StreamRead>>
{
    public override void Configure() => Get("/activities/{id:int}/streams");

    public override async Task HandleAsync(ActivityIdRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (!await db.Activities.AnyAsync(a => a.Id == req.Id && a.UserId == userId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var streams = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == req.Id).OrderBy(s => s.StreamType).ToListAsync(ct);
        await Send.OkAsync(streams.Select(s => new StreamRead(s.StreamType, JsonDocument.Parse(s.Waypoints).RootElement)).ToList(), ct);
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
        var activity = await db.Activities.FirstOrDefaultAsync(a => a.Id == req.Id && a.UserId == userId, ct);
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

public sealed class DeleteActivityEndpoint(AppDb db) : Endpoint<ActivityIdRequest>
{
    public override void Configure() => Delete("/activities/{id:int}");

    public override async Task HandleAsync(ActivityIdRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (await db.Activities.Where(a => a.Id == req.Id && a.UserId == userId).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
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
        RuleFor(x => x.File.Length).InclusiveBetween(1, UploadActivityEndpoint.MaxUploadBytes).WithMessage("File is empty or larger than 50 MB");
        RuleFor(x => Path.GetExtension(x.File.FileName).ToLowerInvariant()).Must(ActivityFileParser.SupportedExtensions.Contains)
            .WithMessage($"Unsupported file type. Allowed: {string.Join(", ", ActivityFileParser.SupportedExtensions)}");
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
        List<Activity> built;
        try
        {
            await using var stream = req.File.OpenReadStream();
            built = ActivityFileParser.Parse(stream, ext).Where(p => p.Points.Count > 0 || p.Summary is not null)
                .Select(p => ActivityBuilder.Build(p, userId, user.Timezone)).ToList();
        }
        catch (InvalidActivityFileException e)
        {
            AddError(e.Message);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Failed to parse uploaded {Extension} file for user {UserId}", ext, userId);
            AddError("Failed to parse activity file");
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        // Re-uploading the same recording is rejected rather than creating a duplicate.
        var starts = built.Select(a => a.StartTime).ToList();
        var existing = await db.Activities.Where(a => a.UserId == userId && starts.Contains(a.StartTime)).Select(a => a.StartTime).ToListAsync(ct);
        var fresh = built.Where(a => !existing.Contains(a.StartTime)).ToList();
        if (fresh.Count == 0)
        {
            AddError("This activity has already been uploaded");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var a in fresh) a.CreatedAt = now;
        db.Activities.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(fresh.Select(ActivityRead.From).ToList(), ct);
    }
}
