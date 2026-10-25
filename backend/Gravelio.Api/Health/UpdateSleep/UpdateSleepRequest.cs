using System.Text.Json;

namespace Gravelio.Api.Health;

public sealed record UpdateSleepRequest(int Id, DateOnly Date, DateTime? SleepStartTimeGmt = null, DateTime? SleepEndTimeGmt = null,
    DateTime? SleepStartTimeLocal = null, DateTime? SleepEndTimeLocal = null, int? TotalSleepSeconds = null, int? NapTimeSeconds = null,
    int? UnmeasurableSleepSeconds = null, int? DeepSleepSeconds = null, int? LightSleepSeconds = null, int? RemSleepSeconds = null,
    int? AwakeSleepSeconds = null, int? AvgHeartRate = null, int? MinHeartRate = null, int? MaxHeartRate = null, int? AvgSpo2 = null,
    int? LowestSpo2 = null, int? HighestSpo2 = null, int? AvgRespiration = null, int? LowestRespiration = null,
    int? HighestRespiration = null, int? AvgStressLevel = null, int? AwakeCount = null, int? RestlessMomentsCount = null,
    JsonElement? SleepStages = null, string? HrvStatus = null, int? RestingHeartRate = null, decimal? AvgSkinTempDeviation = null,
    int? AvgSleepStress = null) : ISleepFields;
