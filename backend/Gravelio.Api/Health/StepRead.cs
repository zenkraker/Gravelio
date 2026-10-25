using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record StepRead(int Id, DateOnly Date, int Steps, string? Source)
{
    public static readonly Expression<Func<HealthStep, StepRead>> Projection = s => new(s.Id, s.Date, s.Steps, s.Source);

    static readonly Func<HealthStep, StepRead> Compiled = Projection.Compile();

    public static StepRead From(HealthStep s) => Compiled(s);
}
