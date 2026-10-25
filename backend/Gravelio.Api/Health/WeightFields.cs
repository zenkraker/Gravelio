using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

/// What a create or update request may set on a reading. <c>Source</c> is not here: only integrations write it.
public interface IWeightFields
{
    DateOnly Date { get; }
    decimal Weight { get; }
    decimal? Bmi { get; }
    decimal? BodyFat { get; }
    decimal? BodyWater { get; }
    decimal? BoneMass { get; }
    decimal? MuscleMass { get; }
    int? PhysiqueRating { get; }
    decimal? VisceralFat { get; }
    int? MetabolicAge { get; }
}

public static class WeightFieldsExtensions
{
    public static void Apply(this HealthWeight w, IWeightFields f) =>
        (w.Date, w.Weight, w.Bmi, w.BodyFat, w.BodyWater, w.BoneMass, w.MuscleMass, w.PhysiqueRating, w.VisceralFat, w.MetabolicAge) =
        (f.Date, f.Weight, f.Bmi, f.BodyFat, f.BodyWater, f.BoneMass, f.MuscleMass, f.PhysiqueRating, f.VisceralFat, f.MetabolicAge);
}
