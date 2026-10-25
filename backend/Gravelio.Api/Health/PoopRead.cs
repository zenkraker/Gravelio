using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record PoopRead(int Id, DateTime DateTime, int? BristolType, string? Color, string? Notes, string? Source)
{
    public static readonly Expression<Func<HealthPoop, PoopRead>> Projection =
        p => new(p.Id, p.DateTime, p.BristolType, p.Color, p.Notes, p.Source);

    static readonly Func<HealthPoop, PoopRead> Compiled = Projection.Compile();

    public static PoopRead From(HealthPoop p) => Compiled(p);
}
