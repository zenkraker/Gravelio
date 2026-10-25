using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdateSleepEndpoint(AppDb db) : Endpoint<UpdateSleepRequest, SleepRead>
{
    public override void Configure() => Put("/health/sleep/{id:int}");

    public override async Task HandleAsync(UpdateSleepRequest req, CancellationToken ct)
    {
        var sleep = await db.HealthSleeps.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (sleep is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        sleep.Apply(req);
        SleepScoring.Apply(sleep);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("There is already a sleep record for that day.", StatusCodes.Status409Conflict);
        await Send.OkAsync(SleepRead.From(sleep), ct);
    }
}
