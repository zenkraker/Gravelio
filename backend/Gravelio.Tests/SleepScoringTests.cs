using Gravelio.Api.Data;
using Gravelio.Api.Health;

namespace Gravelio.Tests;

public class SleepScoringTests
{
    [Theory]
    [InlineData(8, 100, "EXCELLENT")]
    [InlineData(7, 90, "EXCELLENT")]
    [InlineData(6, 70, "GOOD")]
    [InlineData(5, 50, "FAIR")]
    [InlineData(4, 39, "POOR")]
    [InlineData(9.5, 79, "GOOD")]
    [InlineData(10.5, 59, "FAIR")]
    [InlineData(12, 44, "POOR")]
    public void Duration_matches_endurain(double hours, int score, string label)
    {
        var (actualScore, actualLabel) = SleepScoring.Duration((int)(hours * 3600));
        Assert.Equal(score, actualScore);
        Assert.Equal(label, actualLabel);
    }

    [Fact]
    public void Optimal_stages_score_high_and_awake_time_lowers_quality()
    {
        Assert.Equal((98, "EXCELLENT"), SleepScoring.Quality(5184, 14400, 6480, 576, 28800));
        Assert.Equal((20, "POOR"), SleepScoring.Quality(null, null, null, null, 21600));
    }

    [Theory]
    [InlineData(null, 50, "FAIR")]
    [InlineData(0, 100, "EXCELLENT")]
    [InlineData(1, 95, "EXCELLENT")]
    [InlineData(2, 89, "GOOD")]
    [InlineData(4, 69, "FAIR")]
    [InlineData(6, 49, "POOR")]
    public void Awake_count_matches_endurain(int? count, int score, string label) =>
        Assert.Equal((score, label), SleepScoring.AwakeCount(count));

    [Theory]
    [InlineData(null, null, 50, "FAIR")]
    [InlineData(20, null, 100, "EXCELLENT")]
    [InlineData(50, null, 70, "GOOD")]
    [InlineData(75, null, 50, "FAIR")]
    [InlineData(100, null, 0, "POOR")]
    [InlineData(20, 10, 75, "GOOD")]
    public void Stress_matches_endurain(int? stress, int? restless, int score, string label) =>
        Assert.Equal((score, label), SleepScoring.Stress(stress, restless));

    [Fact]
    public void Overall_and_apply_set_every_label()
    {
        var sleep = new HealthSleep
        {
            TotalSleepSeconds = 28800,
            DeepSleepSeconds = 5184,
            LightSleepSeconds = 14400,
            RemSleepSeconds = 6480,
            AwakeSleepSeconds = 576,
            AwakeCount = 1,
            AvgSleepStress = 20,
        };
        SleepScoring.Apply(sleep);

        Assert.Equal(98, sleep.SleepScoreOverall);
        Assert.Equal("EXCELLENT", sleep.SleepScoreDuration);
        Assert.Equal("EXCELLENT", sleep.SleepScoreQuality);
        Assert.Equal("EXCELLENT", sleep.AwakeCountScore);
        Assert.Equal("EXCELLENT", sleep.RemPercentageScore);
        Assert.Equal("EXCELLENT", sleep.DeepPercentageScore);
        Assert.Equal("EXCELLENT", sleep.LightPercentageScore);
        Assert.Equal("EXCELLENT", sleep.SleepStressScore);
        Assert.Equal(98, SleepScoring.Overall(28800, 5184, 14400, 6480, 576, 1, null, 20));
    }
}
