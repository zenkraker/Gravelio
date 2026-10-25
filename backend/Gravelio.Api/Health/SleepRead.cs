using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record SleepRead(int Id, DateOnly Date, DateTime? SleepStartTimeGmt, DateTime? SleepEndTimeGmt, DateTime? SleepStartTimeLocal,
    DateTime? SleepEndTimeLocal, int? TotalSleepSeconds, int? NapTimeSeconds, int? UnmeasurableSleepSeconds, int? DeepSleepSeconds,
    int? LightSleepSeconds, int? RemSleepSeconds, int? AwakeSleepSeconds, int? AvgHeartRate, int? MinHeartRate, int? MaxHeartRate,
    int? AvgSpo2, int? LowestSpo2, int? HighestSpo2, int? AvgRespiration, int? LowestRespiration, int? HighestRespiration,
    int? AvgStressLevel, int? AwakeCount, int? RestlessMomentsCount, int? SleepScoreOverall, string? SleepScoreDuration,
    string? SleepScoreQuality, string? GarminconnectSleepId, JsonElement? SleepStages, string? Source, string? HrvStatus,
    int? RestingHeartRate, decimal? AvgSkinTempDeviation, string? AwakeCountScore, string? RemPercentageScore,
    string? DeepPercentageScore, string? LightPercentageScore, int? AvgSleepStress, string? SleepStressScore)
{
    public static SleepRead From(HealthSleep s) => new(s.Id, s.Date, s.SleepStartTimeGmt, s.SleepEndTimeGmt, s.SleepStartTimeLocal,
        s.SleepEndTimeLocal, s.TotalSleepSeconds, s.NapTimeSeconds, s.UnmeasurableSleepSeconds, s.DeepSleepSeconds, s.LightSleepSeconds,
        s.RemSleepSeconds, s.AwakeSleepSeconds, s.AvgHeartRate, s.MinHeartRate, s.MaxHeartRate, s.AvgSpo2, s.LowestSpo2, s.HighestSpo2,
        s.AvgRespiration, s.LowestRespiration, s.HighestRespiration, s.AvgStressLevel, s.AwakeCount, s.RestlessMomentsCount,
        s.SleepScoreOverall, s.SleepScoreDuration, s.SleepScoreQuality, s.GarminconnectSleepId, ParseStages(s.SleepStages), s.Source,
        s.HrvStatus, s.RestingHeartRate, s.AvgSkinTempDeviation, s.AwakeCountScore, s.RemPercentageScore, s.DeepPercentageScore,
        s.LightPercentageScore, s.AvgSleepStress, s.SleepStressScore);

    static JsonElement? ParseStages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
