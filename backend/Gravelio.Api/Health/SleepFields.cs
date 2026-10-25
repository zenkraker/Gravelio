using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

/// What a create or update request may set. Scores, source and the Garmin id are server-owned.
public interface ISleepFields
{
    DateOnly Date { get; }
    DateTime? SleepStartTimeGmt { get; }
    DateTime? SleepEndTimeGmt { get; }
    DateTime? SleepStartTimeLocal { get; }
    DateTime? SleepEndTimeLocal { get; }
    int? TotalSleepSeconds { get; }
    int? NapTimeSeconds { get; }
    int? UnmeasurableSleepSeconds { get; }
    int? DeepSleepSeconds { get; }
    int? LightSleepSeconds { get; }
    int? RemSleepSeconds { get; }
    int? AwakeSleepSeconds { get; }
    int? AvgHeartRate { get; }
    int? MinHeartRate { get; }
    int? MaxHeartRate { get; }
    int? AvgSpo2 { get; }
    int? LowestSpo2 { get; }
    int? HighestSpo2 { get; }
    int? AvgRespiration { get; }
    int? LowestRespiration { get; }
    int? HighestRespiration { get; }
    int? AvgStressLevel { get; }
    int? AwakeCount { get; }
    int? RestlessMomentsCount { get; }
    JsonElement? SleepStages { get; }
    string? HrvStatus { get; }
    int? RestingHeartRate { get; }
    decimal? AvgSkinTempDeviation { get; }
    int? AvgSleepStress { get; }
}

public static class SleepFieldsExtensions
{
    // Npgsql's timestamptz needs an explicit UTC kind; a datetime-local from the SPA arrives unspecified.
    static DateTime? Utc(DateTime? value)
    {
        if (value is null) return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
    }

    public static void Apply(this HealthSleep s, ISleepFields f)
    {
        (s.Date, s.SleepStartTimeGmt, s.SleepEndTimeGmt, s.SleepStartTimeLocal, s.SleepEndTimeLocal, s.TotalSleepSeconds, s.NapTimeSeconds,
            s.UnmeasurableSleepSeconds, s.DeepSleepSeconds, s.LightSleepSeconds, s.RemSleepSeconds, s.AwakeSleepSeconds, s.AvgHeartRate,
            s.MinHeartRate, s.MaxHeartRate, s.AvgSpo2, s.LowestSpo2, s.HighestSpo2, s.AvgRespiration, s.LowestRespiration,
            s.HighestRespiration, s.AvgStressLevel, s.AwakeCount, s.RestlessMomentsCount, s.HrvStatus, s.RestingHeartRate,
            s.AvgSkinTempDeviation, s.AvgSleepStress) =
        (f.Date, Utc(f.SleepStartTimeGmt), Utc(f.SleepEndTimeGmt), Utc(f.SleepStartTimeLocal), Utc(f.SleepEndTimeLocal),
            f.TotalSleepSeconds, f.NapTimeSeconds, f.UnmeasurableSleepSeconds, f.DeepSleepSeconds, f.LightSleepSeconds, f.RemSleepSeconds,
            f.AwakeSleepSeconds, f.AvgHeartRate, f.MinHeartRate, f.MaxHeartRate, f.AvgSpo2, f.LowestSpo2, f.HighestSpo2,
            f.AvgRespiration, f.LowestRespiration, f.HighestRespiration, f.AvgStressLevel, f.AwakeCount, f.RestlessMomentsCount,
            f.HrvStatus, f.RestingHeartRate, f.AvgSkinTempDeviation, f.AvgSleepStress);
        s.SleepStages = f.SleepStages is { } stages && stages.ValueKind != JsonValueKind.Null ? stages.GetRawText() : null;
    }
}
