using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed record GearRead(int Id, string Nickname, string? Brand, string? Model, int GearType, bool Active, double InitialDistance,
    decimal? PurchaseValue, DateTime CreatedAt, double TotalDistance, int ActivityCount);

public static class GearQueries
{
    /// Reads gear with its usage in one SQL query. Distance and count come only from the owner's own activities.
    public static IQueryable<GearRead> ToRead(this IQueryable<Gear> gears, AppDb db) => gears.Select(g => new GearRead(
        g.Id, g.Nickname, g.Brand, g.Model, g.GearType, g.Active, g.InitialDistance, g.PurchaseValue, g.CreatedAt,
        g.InitialDistance + (db.Activities.Where(a => a.GearId == g.Id && a.UserId == g.UserId).Sum(a => (int?)a.Distance) ?? 0),
        db.Activities.Count(a => a.GearId == g.Id && a.UserId == g.UserId)));

    /// Escapes LIKE wildcards (with backslash, which the query must name as its escape character) so a search for "50%" matches the text "50%", not everything.
    public static string ContainsPattern(string text) => "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

public sealed class ListGearsRequest : PagedRequest
{
    /// Nickname contains this text, ignoring case.
    public string? Q { get; init; }

    [BindFrom("gear_type")]
    public int? GearType { get; init; }

    public bool? Active { get; init; }
}

public sealed class ListGearsValidator : PagedValidator<ListGearsRequest>
{
    public ListGearsValidator()
    {
        RuleFor(x => x.Q).MaximumLength(250);
        RuleFor(x => x.GearType!.Value).Must(GearTypes.IsValid).When(x => x.GearType is not null).WithMessage("Unknown gear type");
    }
}

public sealed class ListGearsEndpoint(AppDb db) : Endpoint<ListGearsRequest, PagedResult<GearRead>>
{
    public override void Configure() => Get("/gears");

    public override async Task HandleAsync(ListGearsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var query = db.Gears.AsNoTracking().Where(g => g.UserId == userId);
        if (!string.IsNullOrWhiteSpace(req.Q)) query = query.Where(g => EF.Functions.ILike(g.Nickname, GearQueries.ContainsPattern(req.Q.Trim()), "\\"));
        if (req.GearType is { } type) query = query.Where(g => g.GearType == type);
        if (req.Active is { } active) query = query.Where(g => g.Active == active);

        await Send.OkAsync(await query.OrderBy(g => g.Nickname).ThenBy(g => g.Id).ToPageAsync(req, page => page.ToRead(db), ct), ct);
    }
}

public sealed class GetGearEndpoint(AppDb db) : Endpoint<IdRequest, GearRead>
{
    public override void Configure() => Get("/gears/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var gear = await db.Gears.AsNoTracking().OwnedBy(User.UserId(), req.Id).ToRead(db).FirstOrDefaultAsync(ct);
        if (gear is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(gear, ct);
    }
}

/// Create and update share one shape; <see cref="Id"/> is the route id on update and ignored on create.
public sealed record GearWriteRequest(int Id, string Nickname, string? Brand, string? Model, int GearType, bool Active = true,
    double InitialDistance = 0, decimal? PurchaseValue = null);

public sealed class GearWriteValidator : Validator<GearWriteRequest>
{
    public GearWriteValidator()
    {
        RuleFor(x => x.Nickname).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Brand).MaximumLength(250);
        RuleFor(x => x.Model).MaximumLength(250);
        RuleFor(x => x.GearType).Must(GearTypes.IsValid).WithMessage("Unknown gear type");
        RuleFor(x => x.InitialDistance).InclusiveBetween(0, 10_000_000); // meters
        RuleFor(x => x.PurchaseValue).InclusiveBetween(0, 1_000_000_000m);
    }
}

public sealed class CreateGearEndpoint(AppDb db, TimeProvider time) : Endpoint<GearWriteRequest, GearRead>
{
    public override void Configure() => Post("/gears");

    public override async Task HandleAsync(GearWriteRequest req, CancellationToken ct)
    {
        var gear = new Gear
        {
            UserId = User.UserId(),
            Nickname = req.Nickname.Trim(),
            Brand = req.Brand,
            Model = req.Model,
            GearType = req.GearType,
            Active = req.Active,
            InitialDistance = req.InitialDistance,
            PurchaseValue = req.PurchaseValue,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Gears.Add(gear);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<GetGearEndpoint>(new { id = gear.Id }, await db.Gears.AsNoTracking().Where(g => g.Id == gear.Id).ToRead(db).FirstAsync(ct), cancellation: ct);
    }
}

public sealed class UpdateGearEndpoint(AppDb db) : Endpoint<GearWriteRequest, GearRead>
{
    public override void Configure() => Put("/gears/{id:int}");

    public override async Task HandleAsync(GearWriteRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var gear = await db.Gears.OwnedBy(userId, req.Id).FirstOrDefaultAsync(ct);
        if (gear is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (req.GearType != gear.GearType)
        {
            // Components and default-gear slots are tied to the gear type, so a type change must not strand them.
            var componentTypes = await db.GearComponents.Where(c => c.GearId == gear.Id).Select(c => c.Type).ToListAsync(ct);
            if (componentTypes.Any(t => !GearTypes.AllowsComponent(req.GearType, t)))
                ThrowError("This gear has components its new type doesn't support. Remove them first.", StatusCodes.Status400BadRequest);
            if (await db.DefaultGears.AnyAsync(d => d.GearId == gear.Id, ct))
                ThrowError("Clear this gear's default-gear settings before changing its type.", StatusCodes.Status400BadRequest);
        }
        (gear.Nickname, gear.Brand, gear.Model, gear.GearType, gear.Active, gear.InitialDistance, gear.PurchaseValue) =
            (req.Nickname.Trim(), req.Brand, req.Model, req.GearType, req.Active, req.InitialDistance, req.PurchaseValue);
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(await db.Gears.AsNoTracking().Where(g => g.Id == gear.Id).ToRead(db).FirstAsync(ct), ct);
    }
}

public sealed class DeleteGearEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/gears/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        // The database unlinks the gear's activities and removes its components and default-gear slots.
        if (await db.Gears.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
