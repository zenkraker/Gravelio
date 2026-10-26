namespace Gravelio.Api.Activities;

/// Everything an anonymous visitor may learn about a public activity, and nothing else: no account, gear, device, visibility or
/// calories. The owner's name appears only when the admin allows it, and fields the owner hides are already blanked.
public sealed record GetPublicActivityResponse(int Id, string? Name, string? Description, int ActivityType, int Distance, DateTime? StartTime,
    string? Timezone, double TotalElapsedTime, double TotalTimerTime, int? ElevationGain, int? ElevationLoss, double? Pace, double? AverageSpeed,
    double? MaxSpeed, int? AveragePower, int? MaxPower, int? NormalizedPower, int? AverageHr, int? MaxHr, int? AverageCad, int? MaxCad,
    string? OwnerName)
{
    public static GetPublicActivityResponse From(ActivityRead a, string? ownerName) => new(a.Id, a.Name, a.Description, a.ActivityType, a.Distance,
        a.StartTime, a.Timezone, a.TotalElapsedTime, a.TotalTimerTime, a.ElevationGain, a.ElevationLoss, a.Pace, a.AverageSpeed, a.MaxSpeed,
        a.AveragePower, a.MaxPower, a.NormalizedPower, a.AverageHr, a.MaxHr, a.AverageCad, a.MaxCad, ownerName);
}
