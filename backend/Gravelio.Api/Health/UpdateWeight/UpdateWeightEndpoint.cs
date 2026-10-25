using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class UpdateWeightEndpoint(AppDb db) : Endpoint<UpdateWeightRequest, WeightRead>
{
    public override void Configure() => Put("/health/weight/{id:int}");

    public override async Task HandleAsync(UpdateWeightRequest req, CancellationToken ct)
    {
        var weight = await db.HealthWeights.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (weight is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        weight.Apply(req);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("There is already a reading for that day.", StatusCodes.Status409Conflict);
        await Send.OkAsync(WeightRead.From(weight), ct);
    }
}
