using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

/// Endurain's sleep scoring, ported without changing the weights or thresholds.
public static class SleepScoring
{
    public const string Excellent = "EXCELLENT", Good = "GOOD", Fair = "FAIR", Poor = "POOR";

    static double StagePercentage(int? stageSeconds, int? totalSeconds) =>
        stageSeconds is null || totalSeconds is null or 0 ? 0 : (double)stageSeconds.Value / totalSeconds.Value * 100;

    static double StageScore(double actualPct, double minOptimal, double maxOptimal, double peakOptimal)
    {
        if (actualPct >= minOptimal && actualPct <= maxOptimal)
        {
            if (actualPct <= peakOptimal)
            {
                var span = peakOptimal - minOptimal;
                return span > 0 ? Math.Min(100, Math.Max(80, 80 + (actualPct - minOptimal) / span * 20)) : 100;
            }
            else
            {
                var span = maxOptimal - peakOptimal;
                return span > 0 ? Math.Min(100, Math.Max(80, 100 - (actualPct - peakOptimal) / span * 20)) : 100;
            }
        }
        if (actualPct < minOptimal) return minOptimal > 0 ? Math.Max(0, 80 * (actualPct / minOptimal)) : 0;
        return Math.Max(0, 80 - (actualPct - maxOptimal) * 4);
    }

    static string Label(int score) => score >= 90 ? Excellent : score >= 70 ? Good : score >= 50 ? Fair : Poor;

    public static (int Score, string Label) Duration(int? totalSleepSeconds)
    {
        if (totalSleepSeconds is null or <= 0) return (0, Poor);
        double hours = totalSleepSeconds.Value / 3600.0;
        if (hours is >= 7.0 and <= 9.0)
        {
            int score = hours <= 8.0 ? (int)(90 + (hours - 7.0) * 10) : (int)(100 - (hours - 8.0) * 10);
            return (score, Excellent);
        }
        if (hours is >= 6.0 and < 7.0) return ((int)(70 + (hours - 6.0) * 20), Good);
        if (hours is > 9.0 and <= 10.0) return ((int)(89 - (hours - 9.0) * 19), Good);
        if (hours is >= 5.0 and < 6.0) return ((int)(50 + (hours - 5.0) * 20), Fair);
        if (hours is > 10.0 and <= 11.0) return ((int)(69 - (hours - 10.0) * 19), Fair);
        if (hours < 5.0) return ((int)Math.Max(0, 49 * (hours / 5.0)), Poor);
        return ((int)Math.Max(0, 49 - (hours - 11.0) * 5), Poor);
    }

    public static (int Score, string Label) Quality(int? deepSeconds, int? lightSeconds, int? remSeconds, int? awakeSeconds, int? totalSeconds)
    {
        if (totalSeconds is null or <= 0) return (0, Poor);
        double deepPct = StagePercentage(deepSeconds, totalSeconds);
        double remPct = StagePercentage(remSeconds, totalSeconds);
        double lightPct = StagePercentage(lightSeconds, totalSeconds);
        double awakePct = StagePercentage(awakeSeconds, totalSeconds);
        double deepScore = StageScore(deepPct, 13.0, 23.0, 18.0);
        double remScore = StageScore(remPct, 20.0, 25.0, 22.5);
        double lightScore = StageScore(lightPct, 45.0, 55.0, 50.0);
        double awakeScore = awakePct <= 5.0 ? 100 - awakePct * 4 : Math.Max(0, 80 - (awakePct - 5.0) * 10);
        int total = (int)(deepScore * 0.25 + remScore * 0.30 + lightScore * 0.25 + awakeScore * 0.20);
        return (total, Label(total));
    }

    public static (int Score, string Label) AwakeCount(int? awakeCount)
    {
        if (awakeCount is null) return (50, Fair);
        if (awakeCount <= 1) return (100 - awakeCount.Value * 5, Excellent);
        if (awakeCount <= 3) return (89 - (awakeCount.Value - 2) * 10, Good);
        if (awakeCount <= 5) return (69 - (awakeCount.Value - 4) * 10, Fair);
        return (Math.Max(0, 49 - (awakeCount.Value - 6) * 5), Poor);
    }

    static (int Score, string Label) Percentage(int? stageSeconds, int? totalSeconds, double min, double max, double peak)
    {
        if (totalSeconds is null or <= 0) return (0, Poor);
        int score = (int)StageScore(StagePercentage(stageSeconds, totalSeconds), min, max, peak);
        return (score, Label(score));
    }

    public static (int Score, string Label) RemPercentage(int? remSeconds, int? totalSeconds) =>
        Percentage(remSeconds, totalSeconds, 20.0, 25.0, 22.5);

    public static (int Score, string Label) DeepPercentage(int? deepSeconds, int? totalSeconds) =>
        Percentage(deepSeconds, totalSeconds, 13.0, 23.0, 18.0);

    public static (int Score, string Label) LightPercentage(int? lightSeconds, int? totalSeconds) =>
        Percentage(lightSeconds, totalSeconds, 45.0, 55.0, 50.0);

    public static (int Score, string Label) Stress(int? avgSleepStress, int? restlessMomentsCount)
    {
        if (avgSleepStress is null) return (50, Fair);
        double stress = avgSleepStress.Value;
        int baseScore = stress <= 25 ? 100
            : stress <= 50 ? 90 - (int)((stress - 25) / 25 * 20)
            : stress <= 75 ? 70 - (int)((stress - 50) / 25 * 20)
            : 50 - (int)((stress - 75) / 25 * 50);
        int penalty = 0;
        if (restlessMomentsCount is { } restless)
            penalty = restless <= 5 ? restless * 2 : 10 + (restless - 5) * 3;
        int final = Math.Max(0, baseScore - penalty);
        return (final, Label(final));
    }

    public static int Overall(int? totalSleepSeconds, int? deepSeconds, int? lightSeconds, int? remSeconds, int? awakeSeconds,
        int? awakeCount, int? restlessMomentsCount, int? avgSleepStress)
    {
        int duration = Duration(totalSleepSeconds).Score;
        int quality = Quality(deepSeconds, lightSeconds, remSeconds, awakeSeconds, totalSleepSeconds).Score;
        int awake = AwakeCount(awakeCount).Score;
        int stress = Stress(avgSleepStress, restlessMomentsCount).Score;
        return Math.Clamp((int)(duration * 0.30 + quality * 0.40 + awake * 0.10 + stress * 0.20), 0, 100);
    }

    public static void Apply(HealthSleep sleep)
    {
        var duration = Duration(sleep.TotalSleepSeconds);
        var quality = Quality(sleep.DeepSleepSeconds, sleep.LightSleepSeconds, sleep.RemSleepSeconds, sleep.AwakeSleepSeconds, sleep.TotalSleepSeconds);
        var awake = AwakeCount(sleep.AwakeCount);
        var rem = RemPercentage(sleep.RemSleepSeconds, sleep.TotalSleepSeconds);
        var deep = DeepPercentage(sleep.DeepSleepSeconds, sleep.TotalSleepSeconds);
        var light = LightPercentage(sleep.LightSleepSeconds, sleep.TotalSleepSeconds);
        var stress = Stress(sleep.AvgSleepStress, sleep.RestlessMomentsCount);
        sleep.SleepScoreOverall = Overall(sleep.TotalSleepSeconds, sleep.DeepSleepSeconds, sleep.LightSleepSeconds, sleep.RemSleepSeconds,
            sleep.AwakeSleepSeconds, sleep.AwakeCount, sleep.RestlessMomentsCount, sleep.AvgSleepStress);
        sleep.SleepScoreDuration = duration.Label;
        sleep.SleepScoreQuality = quality.Label;
        sleep.AwakeCountScore = awake.Label;
        sleep.RemPercentageScore = rem.Label;
        sleep.DeepPercentageScore = deep.Label;
        sleep.LightPercentageScore = light.Label;
        sleep.SleepStressScore = stress.Label;
    }
}
