using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Dynastream.Fit;

namespace Gravelio.Api.Activities.Import;

public class InvalidActivityFileException(string message) : Exception(message);

public record TrackPoint(System.DateTime Time, int Segment, double? Lat, double? Lon, double? Ele, int? Hr, int? Cad, int? Power, double? Temp);

/// Session totals reported by the device (FIT); null fields are derived from the points instead.
public record DeviceSummary(
    System.DateTime StartTime, double TotalElapsedTime, double? TotalTimerTime, double? Distance, int? Calories,
    int? AvgHr, int? MaxHr, int? AvgCad, int? MaxCad, int? AvgPower, int? MaxPower, int? NormalizedPower,
    int? ElevationGain, int? ElevationLoss, double? AvgSpeed, double? MaxSpeed);

public class ParsedActivity
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Sport { get; set; }
    public int? Calories { get; set; }
    public double? Distance { get; set; }
    public string? TrackerManufacturer { get; set; }
    public string? TrackerModel { get; set; }
    public DeviceSummary? Summary { get; set; }
    public List<TrackPoint> Points { get; } = [];
}

public static class ActivityFileParser
{
    public static readonly string[] SupportedExtensions = [".gpx", ".tcx", ".fit"];

    public static List<ParsedActivity> Parse(Stream stream, string extension) => extension.ToLowerInvariant() switch
    {
        ".gpx" => [ParseGpx(LoadXml(stream))],
        ".tcx" => [ParseTcx(LoadXml(stream))],
        ".fit" => ParseFit(stream),
        _ => throw new InvalidActivityFileException($"Unsupported file type '{extension}'"),
    };

    static XDocument LoadXml(Stream stream)
    {
        // DTDs are refused so uploaded XML can't trigger entity expansion or external fetches.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader);
        }
        catch (XmlException e)
        {
            throw new InvalidActivityFileException($"Invalid XML: {e.Message}");
        }
    }

    static IEnumerable<XElement> Kids(XElement? e, string name) =>
        e?.Elements().Where(x => x.Name.LocalName == name) ?? [];

    static XElement? Kid(XElement? e, string name) => Kids(e, name).FirstOrDefault();

    static double? Num(string? s) =>
        double.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;

    static int? Int(string? s) => Num(s) is { } v ? (int)v : null;

    static System.DateTime? Time(string? s) =>
        System.DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    // Plausible elevation range; anything else is a sentinel/garbage reading.
    static double? Elevation(double? e) => e is >= -9999.99 and <= 9999.99 ? e : null;

    static ParsedActivity ParseGpx(XDocument doc)
    {
        var root = doc.Root!;
        var tracks = Kids(root, "trk").ToList();
        if (tracks.Count == 0) throw new InvalidActivityFileException("Invalid GPX file - no tracks found");
        var metadata = Kid(root, "metadata");
        var parsed = new ParsedActivity
        {
            Name = Kid(tracks[0], "name")?.Value ?? Kid(metadata, "name")?.Value ?? Kid(root, "name")?.Value,
            Description = Kid(tracks[0], "desc")?.Value ?? Kid(metadata, "desc")?.Value,
            Sport = Kid(tracks[0], "type")?.Value,
        };
        int segment = 0;
        foreach (var track in tracks)
        {
            foreach (var cal in Kid(track, "extensions")?.Descendants().Where(d => d.Name.LocalName.Equals("calories", StringComparison.OrdinalIgnoreCase)) ?? [])
                if (Int(cal.Value) is { } c) parsed.Calories = (parsed.Calories ?? 0) + c;
            var segments = Kids(track, "trkseg").ToList();
            if (segments.Count == 0) throw new InvalidActivityFileException("Invalid GPX file - no segments found");
            foreach (var seg in segments)
            {
                foreach (var pt in Kids(seg, "trkpt"))
                {
                    // Points without time (e.g. OsmAnd exports) are skipped.
                    if (Time(Kid(pt, "time")?.Value) is not { } time) continue;
                    int? hr = null, cad = null, power = null;
                    foreach (var ext in Kid(pt, "extensions")?.Descendants() ?? [])
                    {
                        switch (ext.Name.LocalName.ToLowerInvariant())
                        {
                            case "hr" or "heartrate" or "heart_rate": hr = Int(ext.Value) ?? 0; break;
                            case "cad" or "cadence": cad = Int(ext.Value) ?? 0; break;
                            case "power" or "powerinwatts": power = Int(ext.Value) ?? 0; break;
                        }
                    }
                    parsed.Points.Add(new TrackPoint(time, segment, Num(pt.Attribute("lat")?.Value), Num(pt.Attribute("lon")?.Value),
                        Elevation(Num(Kid(pt, "ele")?.Value)), hr, cad, power is 0 ? null : power, null));
                }
                segment++;
            }
        }
        if (parsed.Points.Count == 0) throw new InvalidActivityFileException("Invalid GPX file - no trackpoints with valid time data found");
        return parsed;
    }

    static ParsedActivity ParseTcx(XDocument doc)
    {
        var activity = Kid(Kid(doc.Root, "Activities"), "Activity")
            ?? throw new InvalidActivityFileException("Invalid TCX file - no activity found");
        var parsed = new ParsedActivity { Sport = activity.Attribute("Sport")?.Value };
        var laps = Kids(activity, "Lap").ToList();
        int segment = 0;
        foreach (var lap in laps)
        {
            if (Int(Kid(lap, "Calories")?.Value) is { } c) parsed.Calories = (parsed.Calories ?? 0) + c;
            if (Num(Kid(lap, "DistanceMeters")?.Value) is { } d) parsed.Distance = (parsed.Distance ?? 0) + d;
            foreach (var track in Kids(lap, "Track"))
            {
                foreach (var tp in Kids(track, "Trackpoint"))
                {
                    if (Time(Kid(tp, "Time")?.Value) is not { } time) continue;
                    var pos = Kid(tp, "Position");
                    var ext = Kid(tp, "Extensions")?.Descendants().ToList() ?? [];
                    int? watts = Int(ext.FirstOrDefault(e => e.Name.LocalName == "Watts")?.Value);
                    int? cad = Int(Kid(tp, "Cadence")?.Value) ?? Int(ext.FirstOrDefault(e => e.Name.LocalName == "RunCadence")?.Value);
                    parsed.Points.Add(new TrackPoint(time, segment, Num(Kid(pos, "LatitudeDegrees")?.Value), Num(Kid(pos, "LongitudeDegrees")?.Value),
                        Elevation(Num(Kid(tp, "AltitudeMeters")?.Value)), Int(Kid(Kid(tp, "HeartRateBpm"), "Value")?.Value), cad,
                        watts is 0 ? null : watts, null));
                }
                segment++;
            }
        }
        if (parsed.Points.Count == 0) throw new InvalidActivityFileException("Invalid TCX file - no trackpoints with valid time data found");
        parsed.Description = Kid(activity, "Notes")?.Value is { Length: > 0 } notes ? notes : null;
        return parsed;
    }

    const double SemicirclesToDegrees = 180.0 / 2147483648.0;

    static List<ParsedActivity> ParseFit(Stream stream)
    {
        using var buffered = new MemoryStream();
        stream.CopyTo(buffered);
        buffered.Position = 0;
        var decode = new Decode();
        if (!decode.IsFIT(buffered)) throw new InvalidActivityFileException("Invalid FIT file");
        buffered.Position = 0;

        var records = new List<RecordMesg>();
        var sessions = new List<SessionMesg>();
        FileIdMesg? fileId = null;
        var broadcaster = new MesgBroadcaster();
        broadcaster.RecordMesgEvent += (_, e) => records.Add((RecordMesg)e.mesg);
        broadcaster.SessionMesgEvent += (_, e) => sessions.Add((SessionMesg)e.mesg);
        broadcaster.FileIdMesgEvent += (_, e) => fileId ??= (FileIdMesg)e.mesg;
        decode.MesgEvent += broadcaster.OnMesg;
        try
        {
            if (!decode.Read(buffered)) throw new InvalidActivityFileException("Invalid FIT file");
        }
        catch (FitException e)
        {
            throw new InvalidActivityFileException($"Invalid FIT file: {e.Message}");
        }
        if (sessions.Count == 0) throw new InvalidActivityFileException("Invalid FIT file - no sessions found");

        var result = new List<ParsedActivity>();
        foreach (var (s, index) in sessions.Select((s, i) => (s, i)))
        {
            var start = s.GetStartTime()?.GetDateTime() ?? s.GetTimestamp().GetDateTime();
            double elapsed = s.GetTotalElapsedTime() ?? 0;
            var end = start.AddSeconds(elapsed);
            var parsed = new ParsedActivity
            {
                Sport = FitSportName(s.GetSport(), s.GetSubSport()),
                TrackerManufacturer = ManufacturerName(fileId?.GetManufacturer()),
                TrackerModel = fileId?.GetProduct()?.ToString(),
                Summary = new DeviceSummary(start, elapsed, s.GetTotalTimerTime(), s.GetTotalDistance(), s.GetTotalCalories(),
                    s.GetAvgHeartRate(), s.GetMaxHeartRate(), s.GetAvgCadence(), s.GetMaxCadence(), s.GetAvgPower(), s.GetMaxPower(),
                    s.GetNormalizedPower(), s.GetTotalAscent(), s.GetTotalDescent(),
                    s.GetEnhancedAvgSpeed() ?? s.GetAvgSpeed(), s.GetEnhancedMaxSpeed() ?? s.GetMaxSpeed()),
            };
            foreach (var r in records)
            {
                var t = r.GetTimestamp().GetDateTime();
                if (t < start || t > end) continue;
                int? lat = r.GetPositionLat(), lon = r.GetPositionLong();
                parsed.Points.Add(new TrackPoint(t, index, lat * SemicirclesToDegrees, lon * SemicirclesToDegrees,
                    Elevation(r.GetEnhancedAltitude() ?? r.GetAltitude()), r.GetHeartRate(), r.GetCadence(), r.GetPower(), r.GetTemperature()));
            }
            result.Add(parsed);
        }
        return result;
    }

    // The SDK exposes manufacturers as ushort constants, so look the name up from the Manufacturer class.
    static string? ManufacturerName(ushort? id) => id is null ? null :
        typeof(Manufacturer).GetFields().FirstOrDefault(f => f.IsLiteral && f.GetValue(null) is ushort v && v == id)?.Name ?? id.ToString();

    static string SnakeCase(string pascal) => Regex.Replace(pascal, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    static string FitSportName(Sport? sport, SubSport? subSport)
    {
        string name = sport is { } sp ? SnakeCase(sp.ToString()) : "workout";
        if (subSport is not { } ss || ss == SubSport.Generic) return name;
        string sub = SnakeCase(ss.ToString());
        return (name, sub) switch
        {
            ("cycling", "virtual_activity") => "virtual_ride",
            ("cycling", "commuting") => "commuting_ride",
            ("cycling", "mixed_surface") => "mixed_surface_ride",
            ("generic", "breathing") => "hiit",
            _ when ActivityTypes.FromName(sub) != ActivityTypes.Workout => sub,
            _ => name,
        };
    }
}
