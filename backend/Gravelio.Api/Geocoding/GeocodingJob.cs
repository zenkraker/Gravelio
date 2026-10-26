using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Geocoding;

public sealed class GeocodingOptions
{
    /// Off by default: turning it on sends the first GPS position of every activity to the geocoding server.
    public bool Enabled { get; set; }

    /// A Nominatim-compatible server. The public one is rate limited to one request a second and wants a real contact in the user agent.
    /// Docker Compose passes unset variables as empty strings, so blank means "use the default".
    [Url]
    public string Host
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? "https://nominatim.openstreetmap.org" : value.Trim();
    } = "https://nominatim.openstreetmap.org";

    public string UserAgent
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? "Gravelio self-hosted (+https://github.com/zenkraker/Gravelio)" : value.Trim();
    } = "Gravelio self-hosted (+https://github.com/zenkraker/Gravelio)";

    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    [Range(0, 60_000)]
    public int MinIntervalMs { get; set; } = 1100;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 5;
}

/// Looks up the place name (city, town, country) of activities that have a route and no place yet. Activities are tried again after a day
/// when a lookup found nothing, so one unreachable server or an ocean crossing does not keep the job busy.
public sealed class GeocodingJob(IServiceScopeFactory scopes, TimeProvider time, IOptions<GeocodingOptions> options, IHttpClientFactory http,
    ILogger<GeocodingJob> logger) : PeriodicJob(scopes, time, logger)
{
    public const string HttpClientName = "geocoding";
    static readonly TimeSpan Retry = TimeSpan.FromDays(1);

    public const int Lock = 0x47454F; // "GEO"

    protected override int LockKey => Lock;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(options.Value.IntervalMinutes);

    protected override bool Enabled => options.Value.Enabled;

    protected override async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDb>();
        var o = options.Value;
        var now = Time.GetUtcNow().UtcDateTime;
        var cutoff = now - Retry;
        var ids = await db.Activities.AsNoTracking()
            .Where(a => a.City == null && a.Town == null && a.Country == null
                        && db.ActivityStreams.Any(s => s.ActivityId == a.Id && s.StreamType == StreamTypes.Map)
                        && !db.ActivityLocationAttempts.Any(t => t.ActivityId == a.Id && t.AttemptedAt > cutoff))
            .OrderBy(a => a.Id).Select(a => a.Id).Take(o.BatchSize).ToListAsync(ct);

        using var client = http.CreateClient(HttpClientName);
        foreach (var id in ids)
        {
            var point = await FirstPointAsync(db, id, ct);
            (string? City, string? Town, string? Country) place = point is { } p ? await ReverseAsync(client, o, p.Lat, p.Lon, ct) : default;
            await db.ActivityLocationAttempts.Where(t => t.ActivityId == id).ExecuteDeleteAsync(ct);
            db.ActivityLocationAttempts.Add(new ActivityLocationAttempt { ActivityId = id, AttemptedAt = Time.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(ct);
            if (place != default)
                await db.Activities.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.City, place.City).SetProperty(a => a.Town, place.Town).SetProperty(a => a.Country, place.Country), ct);
            db.ChangeTracker.Clear();
            // The public server allows one request a second.
            if (o.MinIntervalMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(o.MinIntervalMs), Time, ct);
        }
    }

    static async Task<(double Lat, double Lon)?> FirstPointAsync(AppDb db, int activityId, CancellationToken ct)
    {
        var waypoints = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == activityId && s.StreamType == StreamTypes.Map)
            .Select(s => s.Waypoints).FirstOrDefaultAsync(ct);
        if (waypoints is null) return null;
        using var doc = JsonDocument.Parse(waypoints);
        foreach (var w in doc.RootElement.EnumerateArray())
            if (w.TryGetProperty("lat", out var lat) && w.TryGetProperty("lon", out var lon) && lat.TryGetDouble(out var la) && lon.TryGetDouble(out var lo))
                return (la, lo);
        return null;
    }

    async Task<(string? City, string? Town, string? Country)> ReverseAsync(HttpClient client, GeocodingOptions o, double lat, double lon, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{o.Host.TrimEnd('/')}/reverse?format=jsonv2&addressdetails=1&zoom=10&lat={lat}&lon={lon}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(o.UserAgent);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Geocoding server answered {Status}", (int)response.StatusCode);
                return default;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("address", out var address)) return default;
            string? Get(params string[] keys) => keys.Select(k => address.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() is { Length: > 0 } s ? s[..Math.Min(s.Length, 250)] : null;
            return (Get("city"), Get("town", "village", "hamlet"), Get("country"));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Geocoding lookup failed");
            return default;
        }
    }
}
