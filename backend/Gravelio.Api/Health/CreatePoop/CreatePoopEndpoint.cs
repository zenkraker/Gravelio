using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class CreatePoopEndpoint(AppDb db) : Endpoint<CreatePoopRequest, PoopRead>
{
    public override void Configure() => Post("/health/poop");

    public override async Task HandleAsync(CreatePoopRequest req, CancellationToken ct)
    {
        var poop = new HealthPoop
        {
            UserId = User.UserId(),
            DateTime = HealthTime.Utc(req.DateTime),
            BristolType = req.BristolType,
            Color = req.Color,
            Notes = req.Notes,
            Source = "manual",
        };
        db.HealthPoops.Add(poop);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<GetPoopEndpoint>(new { id = poop.Id }, PoopRead.From(poop), cancellation: ct);
    }
}
