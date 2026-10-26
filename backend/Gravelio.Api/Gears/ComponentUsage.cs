using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

static class ComponentUsage
{
    /// Distance covered on the gear from the day a component was bought until the day it was retired, in meters.
    /// Days are compared in UTC, so a ride near midnight can land on the neighbouring day.
    // shortcut: loads the gear's rides and sums them here, because Npgsql can't compare a date with a timestamptz in SQL; move to SQL if a gear passes ~50k rides.
    public static async Task<Dictionary<int, double>> UsedAsync(AppDb db, int userId, int gearId, IReadOnlyCollection<GearComponent> components, CancellationToken ct)
    {
        if (components.Count == 0) return [];
        var rides = await db.Activities.AsNoTracking().Where(a => a.GearId == gearId && a.UserId == userId && !a.IsHidden)
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
