using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdateFastingEndpoint(AppDb db) : Endpoint<UpdateFastingRequest, FastingRead>
{
    public override void Configure() => Put("/health/fasting/{id:int}");

    public override async Task HandleAsync(UpdateFastingRequest req, CancellationToken ct)
    {
        var fast = await db.HealthFastings.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (fast is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        fast.FastStartTime = HealthTime.Utc(req.FastStartTime);
        fast.FastEndTime = HealthTime.Utc(req.FastEndTime);
        fast.TargetDurationSeconds = req.TargetDurationSeconds;
        fast.ActualDurationSeconds = req.ActualDurationSeconds ?? (fast.FastEndTime is { } end ? (int)(end - fast.FastStartTime).TotalSeconds : null);
        fast.FastingType = req.FastingType;
        fast.Status = req.Status;
        fast.Notes = req.Notes;
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("You already have an active fast.", StatusCodes.Status409Conflict);
        await Send.OkAsync(FastingRead.From(fast), ct);
    }
}
