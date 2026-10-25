using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gravelio.Api;

/// A request that carries only the id from the route.
public sealed record IdRequest(int Id);

/// Base for every list request. Derive from it and a <see cref="PagedValidator{T}"/>; the page size is capped at 100.
public abstract class PagedRequest
{
    public int Page { get; init; } = 1;

    [BindFrom("page_size")]
    public int PageSize { get; init; } = 20;
}

public abstract class PagedValidator<T> : Validator<T> where T : PagedRequest
{
    protected PagedValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public static class QueryExtensions
{
    /// One page of an ordered query. <paramref name="project"/> maps the page's entities to the DTO, so counting still runs on the plain entity query.
    public static async Task<PagedResult<TRead>> ToPageAsync<TEntity, TRead>(this IOrderedQueryable<TEntity> query, PagedRequest req,
        Func<IQueryable<TEntity>, IQueryable<TRead>> project, CancellationToken ct) =>
        new(await project(query.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize)).ToListAsync(ct), await query.CountAsync(ct), req.Page, req.PageSize);

    /// Saves, and answers false instead of throwing when a unique index rejected the write (a parallel request got there first).
    public static async Task<bool> TrySaveUniqueAsync(this AppDb db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    // A user's own record, or nothing: another user's record is indistinguishable from a missing one.
    public static IQueryable<Activity> OwnedBy(this IQueryable<Activity> activities, int userId, int id) =>
        activities.Where(a => a.Id == id && a.UserId == userId);

    public static IQueryable<Gear> OwnedBy(this IQueryable<Gear> gears, int userId, int id) =>
        gears.Where(g => g.Id == id && g.UserId == userId);

    public static IQueryable<HealthWeight> OwnedBy(this IQueryable<HealthWeight> weights, int userId, int id) =>
        weights.Where(w => w.Id == id && w.UserId == userId);

    public static IQueryable<HealthStep> OwnedBy(this IQueryable<HealthStep> steps, int userId, int id) =>
        steps.Where(s => s.Id == id && s.UserId == userId);

    public static IQueryable<GearComponent> OwnedBy(this IQueryable<GearComponent> components, int userId, int gearId, int id) =>
        components.Where(c => c.Id == id && c.GearId == gearId && c.Gear.UserId == userId);

    /// Escapes LIKE wildcards (with backslash, which the query must name as its escape character) so a search for "50%" matches the text "50%", not everything.
    public static string ContainsPattern(string text) => "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
