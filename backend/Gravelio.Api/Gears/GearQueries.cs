using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

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
