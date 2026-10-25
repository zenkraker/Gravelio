using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class CompleteFastingEndpoint(AppDb db) : Endpoint<CompleteFastingRequest, FastingRead>
{
    public override void Configure() => Post("/health/fasting/{id:int}/complete");

    public override async Task HandleAsync(CompleteFastingRequest req, CancellationToken ct)
    {
        var fast = await db.HealthFastings.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (fast is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (fast.Status != FastingStatuses.InProgress) ThrowError("This fast is no longer in progress.", StatusCodes.Status400BadRequest);
        var end = HealthTime.Utc(req.FastEndTime);
        if (end <= fast.FastStartTime) ThrowError("The fast cannot end before it starts.", StatusCodes.Status400BadRequest);
        fast.FastEndTime = end;
        fast.Status = req.Status;
        fast.ActualDurationSeconds = (int)(end - fast.FastStartTime).TotalSeconds;
        if (!string.IsNullOrWhiteSpace(req.Notes)) fast.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(FastingRead.From(fast), ct);
    }
}
