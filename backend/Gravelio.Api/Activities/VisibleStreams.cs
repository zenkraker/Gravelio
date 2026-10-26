using System.Text.Json;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

/// The one place an activity's streams are read for a viewer, so every route that serves them applies the owner's privacy
/// settings the same way: a stream with no hide flag is never shown, and a hidden start time turns timestamps into offsets.
public static class VisibleStreams
{
    // Clone lets the JsonDocument (and its pooled buffers) be released right away.
    static JsonElement ParseWaypoints(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static async Task<List<StreamRead>> ReadAsync(AppDb db, int activityId, int ownerId, DateTime start, HiddenDetails? hiddenOverride, bool isOwner, CancellationToken ct)
    {
        var settings = isOwner ? null : (await PrivacyMask.ForOwnerAsync(db, ownerId, ct)).Effective(hiddenOverride);
        var hideStart = !isOwner && settings is { HideActivityStartTime: true };
        var streams = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == activityId).OrderBy(s => s.StreamType).ToListAsync(ct);
        return streams.Where(s => PrivacyMask.IsStreamVisible(s.StreamType, settings, isOwner))
            .Select(s => new StreamRead(s.StreamType, hideStart ? PrivacyMask.RelativeWaypoints(s.Waypoints, start) : ParseWaypoints(s.Waypoints)))
            .ToList();
    }
}
