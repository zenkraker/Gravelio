using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gravelio.Api.Activities;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Privacy;

public static class PrivacyMask
{
    public static async Task<UserPrivacySettings?> ForOwnerAsync(AppDb db, int ownerId, CancellationToken ct) =>
        await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == ownerId, ct);

    /// Owners always see their own activity; other viewers get the owner's hidden fields blanked.
    public static ActivityRead Apply(ActivityRead activity, UserPrivacySettings? settings, bool isOwner)
    {
        settings = settings.Effective(activity.HiddenOverride);
        if (isOwner || settings is null) return activity;
        return activity with
        {
            StartTime = settings.HideActivityStartTime ? null : activity.StartTime,
            EndTime = settings.HideActivityStartTime ? null : activity.EndTime,
            GearId = settings.HideActivityGear ? null : activity.GearId,
            // A place name is as revealing as the map it comes from.
            City = settings.HideActivityLocation || settings.HideActivityMap ? null : activity.City,
            Town = settings.HideActivityLocation || settings.HideActivityMap ? null : activity.Town,
            Country = settings.HideActivityLocation || settings.HideActivityMap ? null : activity.Country,
            AverageHr = settings.HideActivityHr ? null : activity.AverageHr,
            MaxHr = settings.HideActivityHr ? null : activity.MaxHr,
            AveragePower = settings.HideActivityPower ? null : activity.AveragePower,
            MaxPower = settings.HideActivityPower ? null : activity.MaxPower,
            NormalizedPower = settings.HideActivityPower ? null : activity.NormalizedPower,
            AverageCad = settings.HideActivityCadence ? null : activity.AverageCad,
            MaxCad = settings.HideActivityCadence ? null : activity.MaxCad,
            AverageSpeed = settings.HideActivitySpeed ? null : activity.AverageSpeed,
            MaxSpeed = settings.HideActivitySpeed ? null : activity.MaxSpeed,
            Pace = settings.HideActivityPace ? null : activity.Pace,
            ElevationGain = settings.HideActivityElevation ? null : activity.ElevationGain,
            ElevationLoss = settings.HideActivityElevation ? null : activity.ElevationLoss,
        };
    }

    /// Masks a page of activity reads, fetching every owner's settings in one query.
    public static async Task<PagedResult<ActivityRead>> ApplyAsync(AppDb db, PagedResult<ActivityRead> page, int requesterUserId, CancellationToken ct)
    {
        var ownerIds = page.Items.Select(a => a.Owner.Id).Distinct().ToList();
        if (ownerIds.Count == 0) return page;
        var settings = await db.UserPrivacySettings.AsNoTracking().Where(p => ownerIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);
        return page with
        {
            Items = page.Items.Select(a => Apply(a, settings.GetValueOrDefault(a.Owner.Id), a.Owner.Id == requesterUserId)).ToList(),
        };
    }

    public static bool IsStreamVisible(int streamType, UserPrivacySettings? settings, bool isOwner)
    {
        if (isOwner) return true;
        var s = settings ?? new UserPrivacySettings();
        return streamType switch
        {
            StreamTypes.Hr => !s.HideActivityHr,
            StreamTypes.Power => !s.HideActivityPower,
            StreamTypes.Cadence => !s.HideActivityCadence,
            StreamTypes.Elevation => !s.HideActivityElevation,
            StreamTypes.Speed => !s.HideActivitySpeed,
            StreamTypes.Pace => !s.HideActivityPace,
            StreamTypes.Map => !s.HideActivityMap && !s.HideActivityLocation,
            // No setting exists for temperature (or any future type), so hide it rather than default to visible.
            _ => false,
        };
    }

    /// Replaces absolute stream timestamps with seconds since the activity start on a fixed epoch.
    /// The activity's start time is hidden, so the charts keep their spacing without exposing when it happened.
    public static JsonElement RelativeWaypoints(string waypoints, DateTime start)
    {
        var array = JsonNode.Parse(waypoints)!.AsArray();
        foreach (var node in array)
        {
            if (node is JsonObject waypoint && waypoint["time"] is JsonValue value && value.TryGetValue<string>(out var time)
                && DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                var offset = Math.Max(0, (timestamp - start).TotalSeconds);
                waypoint["time"] = DateTime.UnixEpoch.AddSeconds(offset).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            }
        }
        return JsonSerializer.SerializeToElement(array);
    }
}
