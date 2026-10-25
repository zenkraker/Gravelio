namespace Gravelio.Api.Health;

public sealed record UpdateWeightRequest(int Id, DateOnly Date, decimal Weight, decimal? Bmi = null, decimal? BodyFat = null, decimal? BodyWater = null,
    decimal? BoneMass = null, decimal? MuscleMass = null, int? PhysiqueRating = null, decimal? VisceralFat = null, int? MetabolicAge = null) : IWeightFields;
