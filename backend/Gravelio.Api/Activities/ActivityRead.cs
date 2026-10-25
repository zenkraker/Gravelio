using System.Linq.Expressions;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed record ActivityRead(int Id, string? Name, string? Description, int Distance, int ActivityType, DateTime StartTime, DateTime EndTime,
    string? Timezone, double TotalElapsedTime, double TotalTimerTime, int? ElevationGain, int? ElevationLoss, double? Pace,
    double? AverageSpeed, double? MaxSpeed, int? AveragePower, int? MaxPower, int? NormalizedPower, int? AverageHr, int? MaxHr,
    int? AverageCad, int? MaxCad, int? Calories, int Visibility, string? TrackerManufacturer, string? TrackerModel, DateTime CreatedAt, int? GearId)
{
    // An expression so EF Core can translate it and select only these columns.
    public static readonly Expression<Func<Activity, ActivityRead>> Projection = a => new ActivityRead(a.Id, a.Name, a.Description, a.Distance,
        a.ActivityType, a.StartTime, a.EndTime, a.Timezone, a.TotalElapsedTime, a.TotalTimerTime, a.ElevationGain, a.ElevationLoss, a.Pace,
        a.AverageSpeed, a.MaxSpeed, a.AveragePower, a.MaxPower, a.NormalizedPower, a.AverageHr, a.MaxHr, a.AverageCad, a.MaxCad, a.Calories,
        a.Visibility, a.TrackerManufacturer, a.TrackerModel, a.CreatedAt, a.GearId);

    static readonly Func<Activity, ActivityRead> Compiled = Projection.Compile();

    public static ActivityRead From(Activity a) => Compiled(a);
}
