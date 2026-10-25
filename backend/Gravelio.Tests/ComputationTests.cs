using Gravelio.Api.Activities;

namespace Gravelio.Tests;

/// Expected values come from running the same inputs through Endurain's original Python (computation.py).
public class ComputationTests
{
    [Fact]
    public void Elevation_gain_and_loss_match_endurain()
    {
        var elevations = Enumerable.Range(0, 500).Select(i => 100 + 10 * Math.Sin(i / 20.0) + ((i * 37) % 7 - 3) * 0.8).ToArray();
        var smoothed = Computation.SmoothElevations(elevations);
        var (gain, loss) = Computation.SumElevationDeltas(smoothed);

        Assert.Equal(67.39524168119966, gain, 9);
        Assert.Equal(74.19343240060861, loss, 9);
        Assert.Equal(102.58380938418907, smoothed[0], 9);
        Assert.Equal(99.38187145106585, smoothed[250], 9);
    }

    [Fact]
    public void Robust_max_speed_ignores_one_spike_like_endurain()
    {
        var speeds = Enumerable.Range(0, 300).Select(i => 5 + ((i * 13) % 11) * 0.3 + (i == 250 ? 40 : 0)).ToList();
        Assert.Equal(7.4, Computation.RobustMax(speeds), 9);
    }

    [Fact]
    public void Normalized_power_matches_endurain()
    {
        var power = Enumerable.Range(0, 300).Select(i => (double)(150 + ((i * 29) % 17) * 10)).ToList();
        Assert.Equal(244.32109153439268, Computation.NormalizedPower(power), 9);
    }
}
