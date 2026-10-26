using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// The one place laps are read for a viewer. Hiding something hides everything derived from it, so each lap field follows the
/// flag of the data it comes from: coordinates the map and location flags, the rest their own metric.
public static class VisibleLaps
{
    public static async Task<List<LapRead>> ReadAsync(AppDb db, int activityId, int ownerId, DateTime start, HiddenDetails? hiddenOverride, bool isOwner, CancellationToken ct)
    {
        var s = isOwner ? null : (await PrivacyMask.ForOwnerAsync(db, ownerId, ct)).Effective(hiddenOverride) ?? new UserPrivacySettings();
        var laps = await db.ActivityLaps.AsNoTracking().Where(l => l.ActivityId == activityId).OrderBy(l => l.Index).ToListAsync(ct);
        return laps.Select(l =>
        {
            bool hidePlace = s is not null && (s.HideActivityMap || s.HideActivityLocation);
            return new LapRead(l.Index, s is { HideActivityStartTime: true } ? null : l.StartTime, Math.Max(0, (l.StartTime - start).TotalSeconds),
                hidePlace ? null : l.StartLat, hidePlace ? null : l.StartLon, hidePlace ? null : l.EndLat, hidePlace ? null : l.EndLon,
                l.TotalElapsedTime, l.TotalTimerTime, l.Distance,
                s is { HideActivityHr: true } ? null : l.AverageHr, s is { HideActivityHr: true } ? null : l.MaxHr,
                s is { HideActivityCadence: true } ? null : l.AverageCad, s is { HideActivityCadence: true } ? null : l.MaxCad,
                s is { HideActivityPower: true } ? null : l.AveragePower, s is { HideActivityPower: true } ? null : l.MaxPower,
                s is { HideActivityElevation: true } ? null : l.ElevationGain, s is { HideActivityElevation: true } ? null : l.ElevationLoss,
                s is { HideActivitySpeed: true } ? null : l.AverageSpeed, s is { HideActivitySpeed: true } ? null : l.MaxSpeed, l.Calories);
        }).ToList();
    }
}
