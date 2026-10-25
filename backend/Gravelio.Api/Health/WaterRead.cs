using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record WaterRead(int Id, DateOnly Date, decimal AmountMl, string? Source)
{
    public static readonly Expression<Func<HealthWater, WaterRead>> Projection = w => new(w.Id, w.Date, w.AmountMl, w.Source);

    static readonly Func<HealthWater, WaterRead> Compiled = Projection.Compile();

    public static WaterRead From(HealthWater w) => Compiled(w);
}
