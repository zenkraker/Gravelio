using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;

namespace Gravelio.Api.Gears;

public sealed record ComponentRead(int Id, int GearId, string Type, string? Brand, string? Model, DateOnly PurchaseDate, DateOnly? RetiredDate,
    bool Active, double? ExpectedDistance, decimal? PurchaseValue, double UsedDistance)
{
    public static ComponentRead From(GearComponent c, double usedDistance) =>
        new(c.Id, c.GearId, c.Type, c.Brand, c.Model, c.PurchaseDate, c.RetiredDate, c.Active, c.ExpectedDistance, c.PurchaseValue, usedDistance);
}
