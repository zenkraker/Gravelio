using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListDefaultGearEndpoint(AppDb db) : EndpointWithoutRequest<List<DefaultGearSlot>>
{
    public override void Configure() => Get("/gears/defaults");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        var chosen = await db.DefaultGears.AsNoTracking().Where(d => d.UserId == userId).ToDictionaryAsync(d => d.ActivityType, d => d.GearId, ct);
        await Send.OkAsync(GearTypes.DefaultSlots.Select(s => new DefaultGearSlot(s.Key, s.Value, chosen.GetValueOrDefault(s.Key) is var id and > 0 ? id : null)).ToList(), ct);
    }
}
