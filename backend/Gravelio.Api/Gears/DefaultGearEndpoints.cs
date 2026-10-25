using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record DefaultGearSlot(int ActivityType, int GearType, int? GearId);

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

/// <see cref="GearId"/> of null clears the default for that activity type.
public sealed record SetDefaultGearRequest(int ActivityType, int? GearId);

public sealed class SetDefaultGearEndpoint(AppDb db) : Endpoint<SetDefaultGearRequest, DefaultGearSlot>
{
    public override void Configure() => Put("/gears/defaults/{activityType:int}");

    public override async Task HandleAsync(SetDefaultGearRequest req, CancellationToken ct)
    {
        if (!GearTypes.DefaultSlots.TryGetValue(req.ActivityType, out var slotGearType))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var userId = User.UserId();
        var existing = await db.DefaultGears.FirstOrDefaultAsync(d => d.UserId == userId && d.ActivityType == req.ActivityType, ct);
        if (req.GearId is null)
        {
            if (existing is not null) db.DefaultGears.Remove(existing);
        }
        else
        {
            var gear = await db.Gears.AsNoTracking().OwnedBy(userId, req.GearId.Value).FirstOrDefaultAsync(ct);
            if (gear is null) ThrowError(r => r.GearId, "Gear not found", StatusCodes.Status404NotFound);
            if (!gear.Active) ThrowError("Choose active gear: this one is retired", StatusCodes.Status400BadRequest);
            if (gear.GearType != slotGearType) ThrowError("That kind of gear doesn't fit this activity type", StatusCodes.Status400BadRequest);
            if (existing is null) db.DefaultGears.Add(new DefaultGear { UserId = userId, ActivityType = req.ActivityType, GearId = gear.Id });
            else existing.GearId = gear.Id;
        }
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("This default was changed at the same time. Try again.", StatusCodes.Status409Conflict);
        await Send.OkAsync(new DefaultGearSlot(req.ActivityType, slotGearType, req.GearId), ct);
    }
}
