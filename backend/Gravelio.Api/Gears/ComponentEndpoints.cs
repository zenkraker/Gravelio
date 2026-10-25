using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record ComponentRead(int Id, int GearId, string Type, string? Brand, string? Model, DateOnly PurchaseDate, DateOnly? RetiredDate,
    bool Active, double? ExpectedDistance, decimal? PurchaseValue, double UsedDistance)
{
    public static ComponentRead From(GearComponent c, double usedDistance) =>
        new(c.Id, c.GearId, c.Type, c.Brand, c.Model, c.PurchaseDate, c.RetiredDate, c.Active, c.ExpectedDistance, c.PurchaseValue, usedDistance);
}

static class ComponentUsage
{
    /// Distance covered on the gear from the day a component was bought until the day it was retired, in meters.
    /// Days are compared in UTC, so a ride near midnight can land on the neighbouring day.
    // shortcut: loads the gear's rides and sums them here, because Npgsql can't compare a date with a timestamptz in SQL; move to SQL if a gear passes ~50k rides.
    public static async Task<Dictionary<int, double>> UsedAsync(AppDb db, int userId, int gearId, IReadOnlyCollection<GearComponent> components, CancellationToken ct)
    {
        if (components.Count == 0) return [];
        var rides = await db.Activities.AsNoTracking().Where(a => a.GearId == gearId && a.UserId == userId)
            .Select(a => new { a.StartTime, a.Distance }).ToListAsync(ct);
        return components.ToDictionary(c => c.Id, c =>
        {
            var from = c.PurchaseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var until = c.RetiredDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return (double)rides.Where(r => r.StartTime >= from && (until is null || r.StartTime < until)).Sum(r => r.Distance);
        });
    }

    public static async Task<ComponentRead> ReadAsync(AppDb db, int userId, GearComponent component, CancellationToken ct) =>
        ComponentRead.From(component, (await UsedAsync(db, userId, component.GearId, [component], ct))[component.Id]);
}

public sealed class ListComponentTypesEndpoint : EndpointWithoutRequest<Dictionary<int, string[]>>
{
    public override void Configure() => Get("/gears/component-types");

    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(Enumerable.Range(GearTypes.Bike, GearTypes.WaterSportsBoard).ToDictionary(t => t, t => GearTypes.ComponentTypesFor(t).Order().ToArray()), ct);
}

public sealed class ListComponentsEndpoint(AppDb db) : Endpoint<IdRequest, List<ComponentRead>>
{
    public override void Configure() => Get("/gears/{id:int}/components");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (!await db.Gears.OwnedBy(User.UserId(), req.Id).AnyAsync(ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var components = await db.GearComponents.AsNoTracking().Where(c => c.GearId == req.Id).OrderBy(c => c.Type).ThenBy(c => c.PurchaseDate).ToListAsync(ct);
        var used = await ComponentUsage.UsedAsync(db, User.UserId(), req.Id, components, ct);
        await Send.OkAsync(components.Select(c => ComponentRead.From(c, used[c.Id])).ToList(), ct);
    }
}

/// <see cref="Id"/> is the gear id and <see cref="ComponentId"/> the component id (zero on create); both come from the route.
public sealed record ComponentWriteRequest(int Id, int ComponentId, string Type, string? Brand, string? Model, DateOnly PurchaseDate,
    DateOnly? RetiredDate = null, bool Active = true, double? ExpectedDistance = null, decimal? PurchaseValue = null);

public sealed class ComponentWriteValidator : Validator<ComponentWriteRequest>
{
    public ComponentWriteValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brand).MaximumLength(250);
        RuleFor(x => x.Model).MaximumLength(250);
        RuleFor(x => x.PurchaseDate).NotEqual(default(DateOnly)).WithMessage("Enter the purchase date");
        RuleFor(x => x.RetiredDate!.Value).GreaterThanOrEqualTo(x => x.PurchaseDate).When(x => x.RetiredDate is not null)
            .WithMessage("A component can't be retired before it was bought");
        RuleFor(x => x.ExpectedDistance).InclusiveBetween(0, 10_000_000); // meters
        RuleFor(x => x.PurchaseValue).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class CreateComponentEndpoint(AppDb db) : Endpoint<ComponentWriteRequest, ComponentRead>
{
    public override void Configure() => Post("/gears/{id:int}/components");

    public override async Task HandleAsync(ComponentWriteRequest req, CancellationToken ct)
    {
        var gear = await db.Gears.OwnedBy(User.UserId(), req.Id).FirstOrDefaultAsync(ct);
        if (gear is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!GearTypes.AllowsComponent(gear.GearType, req.Type)) ThrowError(r => r.Type, "This gear doesn't support that component type", StatusCodes.Status400BadRequest);
        var component = new GearComponent
        {
            GearId = gear.Id,
            Type = req.Type,
            Brand = req.Brand,
            Model = req.Model,
            PurchaseDate = req.PurchaseDate,
            RetiredDate = req.RetiredDate,
            // A retired component is no longer in use, whatever the caller sent.
            Active = req.Active && req.RetiredDate is null,
            ExpectedDistance = req.ExpectedDistance,
            PurchaseValue = req.PurchaseValue,
        };
        db.GearComponents.Add(component);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<ListComponentsEndpoint>(new { id = gear.Id }, await ComponentUsage.ReadAsync(db, User.UserId(), component, ct), cancellation: ct);
    }
}

public sealed class UpdateComponentEndpoint(AppDb db) : Endpoint<ComponentWriteRequest, ComponentRead>
{
    public override void Configure() => Put("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(ComponentWriteRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var component = await db.GearComponents.Include(c => c.Gear).OwnedBy(userId, req.Id, req.ComponentId).FirstOrDefaultAsync(ct);
        if (component is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!GearTypes.AllowsComponent(component.Gear.GearType, req.Type)) ThrowError(r => r.Type, "This gear doesn't support that component type", StatusCodes.Status400BadRequest);
        (component.Type, component.Brand, component.Model, component.PurchaseDate, component.RetiredDate, component.Active, component.ExpectedDistance, component.PurchaseValue) =
            (req.Type, req.Brand, req.Model, req.PurchaseDate, req.RetiredDate, req.Active && req.RetiredDate is null, req.ExpectedDistance, req.PurchaseValue);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(await ComponentUsage.ReadAsync(db, userId, component, ct), ct);
    }
}

public sealed record ComponentIdRequest(int Id, int ComponentId);

public sealed class DeleteComponentEndpoint(AppDb db) : Endpoint<ComponentIdRequest>
{
    public override void Configure() => Delete("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(ComponentIdRequest req, CancellationToken ct)
    {
        if (await db.GearComponents.OwnedBy(User.UserId(), req.Id, req.ComponentId).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
