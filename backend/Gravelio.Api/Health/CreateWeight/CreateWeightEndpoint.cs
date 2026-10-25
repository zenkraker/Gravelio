using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

/// One reading per day: a second post for the same date replaces that day's reading (200) instead of adding one (201).
public sealed class CreateWeightEndpoint(AppDb db) : Endpoint<CreateWeightRequest, WeightRead>
{
    public override void Configure() => Post("/health/weight");

    public override async Task HandleAsync(CreateWeightRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var existing = await db.HealthWeights.FirstOrDefaultAsync(w => w.UserId == userId && w.Date == req.Date, ct);
        var created = existing is null;
        var weight = existing ?? db.HealthWeights.Add(new HealthWeight { UserId = userId }).Entity;
        weight.Apply(req);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("A reading for this day was saved at the same time. Try again.", StatusCodes.Status409Conflict);
        if (created) await Send.CreatedAtAsync<ListWeightEndpoint>(null, WeightRead.From(weight), cancellation: ct);
        else await Send.OkAsync(WeightRead.From(weight), ct);
    }
}
