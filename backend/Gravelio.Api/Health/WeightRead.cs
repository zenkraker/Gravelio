using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record WeightRead(int Id, DateOnly Date, decimal Weight, decimal? Bmi, decimal? BodyFat, decimal? BodyWater, decimal? BoneMass,
    decimal? MuscleMass, int? PhysiqueRating, decimal? VisceralFat, int? MetabolicAge, string? Source)
{
    public static readonly Expression<Func<HealthWeight, WeightRead>> Projection = w => new(w.Id, w.Date, w.Weight, w.Bmi, w.BodyFat, w.BodyWater,
        w.BoneMass, w.MuscleMass, w.PhysiqueRating, w.VisceralFat, w.MetabolicAge, w.Source);

    static readonly Func<HealthWeight, WeightRead> Compiled = Projection.Compile();

    public static WeightRead From(HealthWeight w) => Compiled(w);
}
