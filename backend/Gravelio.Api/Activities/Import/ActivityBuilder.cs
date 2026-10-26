using System.Globalization;
using System.Text.Json;
using Gravelio.Api.Data;
using GeoTimeZone;

namespace Gravelio.Api.Activities.Import;

/// Turns a parsed file into an Activity with its streams, preferring device totals when present.
public static class ActivityBuilder
{
    public static Activity Build(ParsedActivity p, int userId, string defaultTimezone)
    {
        var points = p.Points;
        var s = p.Summary;
        int type = ActivityTypes.FromName(p.Sport);
        // Invariant: stream timestamps are a wire contract, not a display value.
        static string Ts(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Speed between consecutive GPS fixes of the same segment; deltas under 1s count as 1s.
        var vel = new List<(DateTime Time, double V)>();
        double gpsDistance = 0;
        TrackPoint? prev = null;
        foreach (var pt in points)
        {
            if (pt.Lat is null || pt.Lon is null) continue;
            if (prev is not null && prev.Segment == pt.Segment)
            {
                double d = Computation.GeodesicMeters(prev.Lat!.Value, prev.Lon!.Value, pt.Lat.Value, pt.Lon.Value);
                gpsDistance += d;
                double dt = (pt.Time - prev.Time).TotalSeconds;
                if (dt > 0) vel.Add((pt.Time, d / Math.Max(dt, 1)));
            }
            prev = pt;
        }

        var gps = points.Where(x => x.Lat is not null && x.Lon is not null).ToList();
        if (s is null && gps.Count < 2 && p.Distance is null)
            throw new InvalidActivityFileException("File has no summary and fewer than two timed GPS points");

        var start = s?.StartTime ?? points[0].Time;
        var end = s is not null ? s.StartTime.AddSeconds(s.TotalElapsedTime) : points[^1].Time;
        double elapsed = (end - start).TotalSeconds;
        double timer = s?.TotalTimerTime ?? elapsed;
        double distance = s?.Distance is > 0 ? s.Distance.Value : p.Distance is > 0 ? p.Distance.Value : gpsDistance;
        if (distance == 0 && s?.AvgSpeed is > 0) distance = s.AvgSpeed.Value * timer;

        var hr = points.Where(x => x.Hr is > 0).Select(x => (x.Time, V: (double)x.Hr!.Value)).ToList();
        var cad = points.Where(x => x.Cad is not null).Select(x => (x.Time, V: (double)x.Cad!.Value)).ToList();
        var power = points.Where(x => x.Power is not null).Select(x => (x.Time, V: (double)x.Power!.Value)).ToList();
        var temp = points.Where(x => x.Temp is not null).Select(x => (x.Time, V: x.Temp!.Value)).ToList();
        var eleRaw = points.Where(x => x.Ele is not null).ToList();
        var eleSmoothed = Computation.SmoothElevations(eleRaw.Select(x => x.Ele!.Value).ToArray());
        var (gain, loss) = Computation.SumElevationDeltas(eleSmoothed);

        static int? Round(double? v) => v is > 0 ? (int)Math.Round(v.Value, MidpointRounding.ToEven) : null;
        static double? Avg(List<(DateTime, double V)> l) => l.Count > 0 ? l.Average(x => x.V) : null;
        static double? Max(List<(DateTime, double V)> l) => l.Count > 0 ? l.Max(x => x.V) : null;

        string timezone = defaultTimezone;
        if (!ActivityTypes.Virtual.Contains(type) && gps.Count > 0)
            timezone = TimeZoneLookup.GetTimeZone(gps[0].Lat!.Value, gps[0].Lon!.Value).Result;

        var activity = new Activity
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(p.Name) ? ActivityTypes.Names.GetValueOrDefault(type, "Workout") : p.Name,
            Description = p.Description,
            ActivityType = type,
            StartTime = TruncateToSecond(start),
            EndTime = TruncateToSecond(end),
            Timezone = timezone,
            TotalElapsedTime = elapsed,
            TotalTimerTime = timer,
            Distance = (int)Math.Round(distance),
            Pace = distance > 0 ? timer / distance : 0,
            AverageSpeed = s?.AvgSpeed ?? Avg(vel) ?? (distance > 0 && timer > 0 ? distance / timer : null),
            MaxSpeed = s?.MaxSpeed ?? (vel.Count > 0 ? Computation.RobustMax(vel.Select(v => v.V).ToList()) : null),
            // A flat route has a real 0 m gain, distinct from no elevation data at all.
            ElevationGain = s?.ElevationGain ?? (eleRaw.Count > 0 ? (int)Math.Round(gain) : null),
            ElevationLoss = s?.ElevationLoss ?? (eleRaw.Count > 0 ? (int)Math.Round(loss) : null),
            // Heart rate is recomputed from the stream when present: devices count signal-loss zeros.
            AverageHr = Round(Avg(hr)) ?? s?.AvgHr,
            MaxHr = Round(Max(hr)) ?? s?.MaxHr,
            AverageCad = s?.AvgCad ?? Round(Avg(cad)),
            MaxCad = s?.MaxCad ?? Round(Max(cad)),
            AveragePower = s?.AvgPower ?? Round(Avg(power)),
            MaxPower = s?.AvgPower is not null ? s.MaxPower : Round(Max(power)),
            NormalizedPower = s?.NormalizedPower ?? Round(Computation.NormalizedPower(power.Select(x => x.V).ToList())),
            Calories = s?.Calories ?? p.Calories,
            TrackerManufacturer = p.TrackerManufacturer,
            TrackerModel = p.TrackerModel,
        };

        activity.Laps.AddRange(p.Laps.OrderBy(l => l.StartTime).Select((l, i) => new ActivityLap
        {
            Index = i + 1,
            StartTime = TruncateToSecond(l.StartTime),
            StartLat = l.StartLat,
            StartLon = l.StartLon,
            EndLat = l.EndLat,
            EndLon = l.EndLon,
            TotalElapsedTime = l.TotalElapsedTime,
            TotalTimerTime = l.TotalTimerTime,
            Distance = l.Distance,
            AverageHr = l.AvgHr,
            MaxHr = l.MaxHr,
            AverageCad = l.AvgCad,
            MaxCad = l.MaxCad,
            AveragePower = l.AvgPower,
            MaxPower = l.MaxPower,
            ElevationGain = l.ElevationGain,
            ElevationLoss = l.ElevationLoss,
            AverageSpeed = l.AvgSpeed,
            MaxSpeed = l.MaxSpeed,
            Calories = l.Calories,
        }));

        void Add(int streamType, IEnumerable<object> waypoints)
        {
            var list = waypoints.ToList();
            if (list.Count > 0) activity.Streams.Add(new ActivityStream { StreamType = streamType, Waypoints = JsonSerializer.Serialize(list) });
        }
        if (hr.Count > 0) Add(StreamTypes.Hr, points.Where(x => x.Hr is not null).Select(x => new { time = Ts(x.Time), hr = x.Hr }));
        if (power.Count > 0) Add(StreamTypes.Power, power.Select(x => new { time = Ts(x.Time), power = x.V }));
        if (cad.Any(x => x.V > 0)) Add(StreamTypes.Cadence, cad.Select(x => new { time = Ts(x.Time), cad = x.V }));
        Add(StreamTypes.Elevation, eleRaw.Select((x, i) => new { time = Ts(x.Time), ele = Math.Round(eleSmoothed[i], 2) }));
        Add(StreamTypes.Speed, vel.Select(x => new { time = Ts(x.Time), vel = x.V }));
        Add(StreamTypes.Pace, vel.Where(x => x.V > 0).Select(x => new { time = Ts(x.Time), pace = 1 / x.V }));
        Add(StreamTypes.Map, gps.Select(x => new { time = Ts(x.Time), lat = x.Lat, lon = x.Lon }));
        Add(StreamTypes.Temperature, temp.Select(x => new { time = Ts(x.Time), temp = x.V }));
        return activity;
    }

    static DateTime TruncateToSecond(DateTime t) =>
        DateTime.SpecifyKind(new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
}
