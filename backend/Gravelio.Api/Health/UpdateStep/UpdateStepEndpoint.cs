using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdateStepEndpoint(AppDb db) : Endpoint<UpdateStepRequest, StepRead>
{
    public override void Configure() => Put("/health/steps/{id:int}");

    public override async Task HandleAsync(UpdateStepRequest req, CancellationToken ct)
    {
        var step = await db.HealthSteps.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (step is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        (step.Date, step.Steps) = (req.Date, req.Steps);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("There is already a steps entry for that day.", StatusCodes.Status409Conflict);
        await Send.OkAsync(StepRead.From(step), ct);
    }
}
