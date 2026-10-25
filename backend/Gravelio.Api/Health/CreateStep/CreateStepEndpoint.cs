using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

/// One entry per day: a second post for the same date replaces that day's count (200) instead of adding one (201).
public sealed class CreateStepEndpoint(AppDb db) : Endpoint<CreateStepRequest, StepRead>
{
    public override void Configure() => Post("/health/steps");

    public override async Task HandleAsync(CreateStepRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var existing = await db.HealthSteps.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == req.Date, ct);
        var created = existing is null;
        var step = existing ?? db.HealthSteps.Add(new HealthStep { UserId = userId }).Entity;
        (step.Date, step.Steps) = (req.Date, req.Steps);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("A steps entry for this day was saved at the same time. Try again.", StatusCodes.Status409Conflict);
        if (created) await Send.CreatedAtAsync<ListStepsEndpoint>(null, StepRead.From(step), cancellation: ct);
        else await Send.OkAsync(StepRead.From(step), ct);
    }
}
