using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

/// One session per day: a second post for the same date replaces that day's record (200) instead of adding one (201).
public sealed class CreateSleepEndpoint(AppDb db) : Endpoint<CreateSleepRequest, SleepRead>
{
    public override void Configure() => Post("/health/sleep");

    public override async Task HandleAsync(CreateSleepRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var existing = await db.HealthSleeps.FirstOrDefaultAsync(s => s.UserId == userId && s.Date == req.Date, ct);
        var created = existing is null;
        var sleep = existing ?? db.HealthSleeps.Add(new HealthSleep { UserId = userId }).Entity;
        sleep.Apply(req);
        SleepScoring.Apply(sleep);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("A sleep record for this day was saved at the same time. Try again.", StatusCodes.Status409Conflict);
        if (created) await Send.CreatedAtAsync<ListSleepEndpoint>(null, SleepRead.From(sleep), cancellation: ct);
        else await Send.OkAsync(SleepRead.From(sleep), ct);
    }
}
