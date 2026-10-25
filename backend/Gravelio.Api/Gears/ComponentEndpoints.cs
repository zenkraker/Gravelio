using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record ComponentRead(int Id, int GearId, string Type, string? Brand, string? Model, DateOnly PurchaseDate, DateOnly? RetiredDate,
    bool Active, double? ExpectedDistance, decimal? PurchaseValue)
{
    public static ComponentRead From(GearComponent c) =>
        new(c.Id, c.GearId, c.Type, c.Brand, c.Model, c.PurchaseDate, c.RetiredDate, c.Active, c.ExpectedDistance, c.PurchaseValue);
}

public sealed record GearComponentsRequest(int Id);

public sealed class ListComponentsEndpoint(AppDb db) : Endpoint<GearComponentsRequest, List<ComponentRead>>
{
    public override void Configure() => Get("/gears/{id:int}/components");

    public override async Task HandleAsync(GearComponentsRequest req, CancellationToken ct)
    {
        if (!await db.Gears.OwnedBy(User.UserId(), req.Id).AnyAsync(ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var components = await db.GearComponents.AsNoTracking().Where(c => c.GearId == req.Id).OrderBy(c => c.Type).ThenBy(c => c.PurchaseDate).ToListAsync(ct);
        await Send.OkAsync(components.Select(ComponentRead.From).ToList(), ct);
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
        RuleFor(x => x.ExpectedDistance!.Value).InclusiveBetween(0, 10_000_000).When(x => x.ExpectedDistance is not null); // meters
        RuleFor(x => x.PurchaseValue!.Value).InclusiveBetween(0, 1_000_000_000m).When(x => x.PurchaseValue is not null);
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
        await Send.CreatedAtAsync<ListComponentsEndpoint>(new { id = gear.Id }, ComponentRead.From(component), cancellation: ct);
    }
}

public sealed class UpdateComponentEndpoint(AppDb db) : Endpoint<ComponentWriteRequest, ComponentRead>
{
    public override void Configure() => Put("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(ComponentWriteRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var component = await db.GearComponents.Include(c => c.Gear)
            .FirstOrDefaultAsync(c => c.Id == req.ComponentId && c.GearId == req.Id && c.Gear.UserId == userId, ct);
        if (component is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!GearTypes.AllowsComponent(component.Gear.GearType, req.Type)) ThrowError(r => r.Type, "This gear doesn't support that component type", StatusCodes.Status400BadRequest);
        (component.Type, component.Brand, component.Model, component.PurchaseDate, component.RetiredDate, component.Active, component.ExpectedDistance, component.PurchaseValue) =
            (req.Type, req.Brand, req.Model, req.PurchaseDate, req.RetiredDate, req.Active && req.RetiredDate is null, req.ExpectedDistance, req.PurchaseValue);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(ComponentRead.From(component), ct);
    }
}

public sealed record ComponentIdRequest(int Id, int ComponentId);

public sealed class DeleteComponentEndpoint(AppDb db) : Endpoint<ComponentIdRequest>
{
    public override void Configure() => Delete("/gears/{id:int}/components/{componentId:int}");

    public override async Task HandleAsync(ComponentIdRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        if (await db.GearComponents.Where(c => c.Id == req.ComponentId && c.GearId == req.Id && c.Gear.UserId == userId).ExecuteDeleteAsync(ct) > 0)
            await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
