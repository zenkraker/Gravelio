namespace Gravelio.Api.Activities;

/// A lap as a viewer may see it: whatever the owner hides is null. <c>StartOffset</c> (seconds from the activity start) stays
/// even when the start time is hidden, so a lap table keeps its order and spacing without saying when it happened.
public sealed record LapRead(int Index, DateTime? StartTime, double StartOffset, double? StartLat, double? StartLon, double? EndLat, double? EndLon,
    double TotalElapsedTime, double? TotalTimerTime, double? Distance, int? AverageHr, int? MaxHr, int? AverageCad, int? MaxCad,
    int? AveragePower, int? MaxPower, int? ElevationGain, int? ElevationLoss, double? AverageSpeed, double? MaxSpeed, int? Calories);
