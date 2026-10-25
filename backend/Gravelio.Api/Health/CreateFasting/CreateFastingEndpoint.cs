using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class CreateFastingEndpoint(AppDb db) : Endpoint<CreateFastingRequest, FastingRead>
{
    public override void Configure() => Post("/health/fasting");

    public override async Task HandleAsync(CreateFastingRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (await db.HealthFastings.AnyAsync(f => f.UserId == userId && f.Status == FastingStatuses.InProgress, ct))
            ThrowError("You already have an active fast. End or cancel it before starting another.", StatusCodes.Status400BadRequest);

        var fast = new HealthFasting
        {
            UserId = userId,
            FastStartTime = HealthTime.Utc(req.FastStartTime),
            FastEndTime = HealthTime.Utc(req.FastEndTime),
            TargetDurationSeconds = req.TargetDurationSeconds,
            FastingType = req.FastingType,
            Status = FastingStatuses.InProgress,
            Notes = req.Notes,
            Source = "manual",
        };
        db.HealthFastings.Add(fast);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("You already have an active fast.", StatusCodes.Status409Conflict);
        await Send.CreatedAtAsync<GetFastingEndpoint>(new { id = fast.Id }, FastingRead.From(fast), cancellation: ct);
    }
}
