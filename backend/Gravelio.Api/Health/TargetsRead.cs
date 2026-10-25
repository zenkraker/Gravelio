using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record TargetsRead(int Id, decimal? Weight, int? Steps, int? Sleep, int? Fasting, decimal? WaterMl, int? PoopCount)
{
    public static TargetsRead From(HealthTarget? target) => target is null
        ? new(0, null, null, null, null, null, null)
        : new(target.Id, target.Weight, target.Steps, target.Sleep, target.Fasting, target.WaterMl, target.PoopCount);
}
