namespace Gravelio.Api.Health;

public sealed record SaveTargetsRequest(decimal? Weight = null, int? Steps = null, int? Sleep = null, int? Fasting = null,
    decimal? WaterMl = null, int? PoopCount = null);
