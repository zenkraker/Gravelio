using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dynastream.Fit;
using Gravelio.Api.Activities;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Data;
using Gravelio.Api.Gears;
using DateTime = System.DateTime;
using Activity = Gravelio.Api.Data.Activity;

namespace Gravelio.Tests;

public class ImportTests
{
    static readonly DateTime Start = new(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);

    // 60 points 10 s apart heading NNE from central Lisbon; geopy sums this to 3618.3876 m.
    static IEnumerable<(DateTime T, double Lat, double Lon, double Ele, int Hr)> Track() =>
        Enumerable.Range(0, 60).Select(i => (Start.AddSeconds(i * 10), 38.7223 + i * 0.0005, -9.1393 + i * 0.0003, 100.0 + i, i == 5 ? 0 : 120 + i % 10));

    static Activity Build(string ext, string content) =>
        ActivityBuilder.Build(ActivityFileParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(content)), ext).Single(), 1, "UTC");

    [Fact]
    public void Geodesic_matches_geopy()
    {
        Assert.Equal(273924.6532138061, Computation.GeodesicMeters(38.7223, -9.1393, 41.1579, -8.6291), 0.001);
        Assert.Equal(111.31949079327357, Computation.GeodesicMeters(0, 0, 0, 0.001), 0.001);
    }

    [Fact]
    public void RobustMax_ignores_single_spike() =>
        Assert.Equal(5, Computation.RobustMax([5, 5, 5, 99, 5, 5, 5]));

    [Fact]
    public void Gpx_is_parsed_into_activity_and_streams()
    {
        var pts = string.Concat(Track().Select(p => string.Create(CultureInfo.InvariantCulture,
            $"<trkpt lat=\"{p.Lat}\" lon=\"{p.Lon}\"><ele>{p.Ele}</ele><time>{p.T:yyyy-MM-ddTHH:mm:ssZ}</time>" +
            $"<extensions><gpxtpx:TrackPointExtension><gpxtpx:hr>{p.Hr}</gpxtpx:hr></gpxtpx:TrackPointExtension></extensions></trkpt>")));
        var gpx = $"""
            <?xml version="1.0"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1" xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
              <trk><name>Morning run</name><type>running</type><trkseg>{pts}</trkseg></trk>
            </gpx>
            """;
        var a = Build(".gpx", gpx);

        Assert.Equal("Morning run", a.Name);
        Assert.Equal(1, a.ActivityType);
        Assert.Equal(3618, a.Distance);
        Assert.Equal(590, a.TotalElapsedTime);
        Assert.Equal("Europe/Lisbon", a.Timezone);
        Assert.Equal(129, a.MaxHr);
        Assert.True(a.ElevationGain is > 40 and < 60, $"gain {a.ElevationGain}");
        Assert.Equal(0, a.ElevationLoss);
        Assert.Equal(590.0 / 3618.3876, a.Pace!.Value, 4);
        Assert.Equal([StreamTypes.Hr, StreamTypes.Elevation, StreamTypes.Speed, StreamTypes.Pace, StreamTypes.Map], a.Streams.Select(s => s.StreamType).Order());
        Assert.Equal(60, JsonDocument.Parse(a.Streams.Single(s => s.StreamType == StreamTypes.Map).Waypoints).RootElement.GetArrayLength());
    }

    [Fact]
    public void Gpx_segment_gaps_do_not_count_as_distance()
    {
        var gpx = $"""
            <gpx xmlns="http://www.topografix.com/GPX/1/1"><trk>
              <trkseg><trkpt lat="0" lon="0"><time>{Start:o}</time></trkpt><trkpt lat="0" lon="0.001"><time>{Start.AddSeconds(30):o}</time></trkpt></trkseg>
              <trkseg><trkpt lat="1" lon="0"><time>{Start.AddSeconds(60):o}</time></trkpt><trkpt lat="1" lon="0.001"><time>{Start.AddSeconds(90):o}</time></trkpt></trkseg>
            </trk></gpx>
            """;
        Assert.InRange(Build(".gpx", gpx).Distance, 222, 223);
    }

    [Fact]
    public void Xml_with_dtd_is_rejected() =>
        Assert.Throws<InvalidActivityFileException>(() => Build(".gpx",
            """<?xml version="1.0"?><!DOCTYPE gpx [<!ENTITY x SYSTEM "file:///etc/passwd">]><gpx><trk><name>&x;</name></trk></gpx>"""));

    [Fact]
    public void Tcx_is_parsed()
    {
        var tps = string.Concat(Track().Select(p => string.Create(CultureInfo.InvariantCulture,
            $"<Trackpoint><Time>{p.T:yyyy-MM-ddTHH:mm:ssZ}</Time><Position><LatitudeDegrees>{p.Lat}</LatitudeDegrees><LongitudeDegrees>{p.Lon}</LongitudeDegrees></Position>" +
            $"<AltitudeMeters>{p.Ele}</AltitudeMeters><HeartRateBpm><Value>{p.Hr}</Value></HeartRateBpm><Cadence>85</Cadence></Trackpoint>")));
        var tcx = $"""
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities><Activity Sport="Biking"><Lap><DistanceMeters>3600</DistanceMeters><Calories>250</Calories><Track>{tps}</Track></Lap></Activity></Activities>
            </TrainingCenterDatabase>
            """;
        var a = Build(".tcx", tcx);
        Assert.Equal(4, a.ActivityType);
        Assert.Equal(3600, a.Distance);
        Assert.Equal(250, a.Calories);
        Assert.Equal(85, a.AverageCad);
    }

    [Fact]
    public void Fit_session_totals_are_used()
    {
        const double toSemicircles = 2147483648.0 / 180.0;
        var ms = new MemoryStream();
        var enc = new Encode(ProtocolVersion.V20);
        enc.Open(ms);
        var fileId = new FileIdMesg();
        fileId.SetType(Dynastream.Fit.File.Activity);
        fileId.SetManufacturer(Manufacturer.Garmin);
        fileId.SetProduct(3121);
        enc.Write(fileId);
        foreach (var p in Track())
        {
            var r = new RecordMesg();
            r.SetTimestamp(new Dynastream.Fit.DateTime(p.T));
            r.SetPositionLat((int)(p.Lat * toSemicircles));
            r.SetPositionLong((int)(p.Lon * toSemicircles));
            r.SetEnhancedAltitude((float)p.Ele);
            r.SetHeartRate((byte)p.Hr);
            r.SetPower(200);
            enc.Write(r);
        }
        var s = new SessionMesg();
        s.SetStartTime(new Dynastream.Fit.DateTime(Start));
        s.SetTimestamp(new Dynastream.Fit.DateTime(Start.AddSeconds(590)));
        s.SetTotalElapsedTime(590);
        s.SetTotalTimerTime(580);
        s.SetTotalDistance(3620);
        s.SetTotalAscent(42);
        s.SetSport(Sport.Cycling);
        s.SetSubSport(SubSport.VirtualActivity);
        enc.Write(s);
        enc.Close();
        ms.Position = 0;

        var a = ActivityBuilder.Build(ActivityFileParser.Parse(ms, ".fit").Single(), 1, "Europe/Berlin");
        Assert.Equal(7, a.ActivityType);
        Assert.Equal("Europe/Berlin", a.Timezone); // virtual rides never take the timezone from GPS
        Assert.Equal(3620, a.Distance);
        Assert.Equal(580, a.TotalTimerTime);
        Assert.Equal(42, a.ElevationGain);
        Assert.Equal(200, a.NormalizedPower);
        Assert.Equal("Garmin", a.TrackerManufacturer);
    }

    [Fact]
    public void Garbage_fit_is_rejected() =>
        Assert.Throws<InvalidActivityFileException>(() => ActivityFileParser.Parse(new MemoryStream([1, 2, 3, 4]), ".fit"));

    static string CsvRow(params (int Column, string Value)[] cells)
    {
        var row = new string[35];
        foreach (var (column, value) in cells) row[column] = value;
        return string.Join(",", row);
    }

    static MemoryStream Zip(params (string Path, byte[] Content)[] files)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    static byte[] Gzip(string text)
    {
        var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true)) gzip.Write(Utf8(text));
        return ms.ToArray();
    }

    static string StravaGpx(string name, string sport) =>
        $"""<gpx xmlns="http://www.topografix.com/GPX/1/1"><trk><name>{name}</name><type>{sport}</type><trkseg>{string.Concat(Track().Select(p => string.Create(CultureInfo.InvariantCulture, $"<trkpt lat=\"{p.Lat}\" lon=\"{p.Lon}\"><ele>{p.Ele}</ele><time>{p.T:yyyy-MM-ddTHH:mm:ssZ}</time></trkpt>")))}</trkseg></trk></gpx>""";

    [Fact]
    public void Strava_export_uses_csv_totals_and_gpx_track()
    {
        var row = CsvRow((0, "1"), (2, "Morning gravel"), (3, "Ciclismo"), (11, "scott"), (12, "activities/1.gpx"),
            (15, "590"), (16, "580"), (17, "3620"), (18, "15"), (19, "6"), (20, "42"), (21, "10"),
            (28, "95"), (29, "85"), (30, "170"), (31, "140"), (32, "400"), (33, "200"), (34, "250"));
        var missing = CsvRow((0, "2"), (12, "activities/missing.gpx"));
        var csv = CsvRow((0, "Activity ID")) + "\n" + row + "\n" + missing + "\n";
        using var zip = Zip(("activities.csv", Utf8(csv)), ("activities/1.gpx", Utf8(StravaGpx("GPX name", "gravel_biking"))));

        var export = StravaExport.Read(zip);
        Assert.Single(export.Errors);
        var a = ActivityBuilder.Build(Assert.Single(export.Activities).Parsed, 1, "UTC");

        Assert.Equal("Morning gravel", a.Name);
        Assert.Equal(5, a.ActivityType); // Strava's gravel_biking
        Assert.Equal(Start, a.StartTime); // the track's first point, not the localised CSV date
        Assert.Equal(3620, a.Distance); // CSV truth, not the summed GPS points
        Assert.Equal(590, a.TotalElapsedTime);
        Assert.Equal(580, a.TotalTimerTime);
        Assert.Equal(42, a.ElevationGain);
        Assert.Equal(170, a.MaxHr);
        Assert.Equal(140, a.AverageHr);
        Assert.Equal(200, a.AveragePower);
        Assert.Equal(250, a.Calories);
    }

    [Fact]
    public void Strava_export_reads_gzipped_recordings()
    {
        var row = CsvRow((0, "1"), (12, "activities/1.gpx.gz"), (15, "590"));
        var csv = CsvRow((0, "Activity ID")) + "\n" + row + "\n";
        using var zip = Zip(("activities.csv", Utf8(csv)), ("activities/1.gpx.gz", Gzip(StravaGpx("Gz ride", "mountain_biking"))));

        var export = StravaExport.Read(zip);
        Assert.Empty(export.Errors);
        var a = ActivityBuilder.Build(Assert.Single(export.Activities).Parsed, 1, "UTC");
        Assert.Equal(6, a.ActivityType);
        Assert.Equal(590, a.TotalElapsedTime);
        Assert.Contains(a.Streams, s => s.StreamType == StreamTypes.Map);
    }

    [Theory]
    [InlineData("gravel_biking", 5)]
    [InlineData("mountain_biking", 6)]
    [InlineData("EMountainBikeRide", 36)]
    public void Strava_sport_names_map_to_activity_types(string sport, int type) =>
        Assert.Equal(type, ActivityTypes.FromName(sport));

    [Fact]
    public void Strava_export_reads_bikes_and_shoes_and_links_them_to_activities()
    {
        var csv = CsvRow((0, "Activity ID")) + "\n" +
            CsvRow((0, "1"), (2, "Gear ride"), (11, "scott"), (12, "activities/1.gpx"), (15, "590")) + "\n";
        var bikes = "Nome della bicicletta,Marca della bicicletta,Modello della bicicletta,Sport predefiniti per la bici\n" +
            "scott,Scott,Speedster gravel 30 eq,Gravel biking\n";
        var shoes = "Nome delle scarpe,Marca delle scarpe,Modello delle scarpe,Sport predefiniti per le scarpe\n" +
            "hoka,Hoka,Clifton 9,Trail running\n";
        using var zip = Zip(("activities.csv", Utf8(csv)), ("activities/1.gpx", Utf8(StravaGpx("Gear ride", "cycling"))),
            ("bikes.csv", Utf8(bikes)), ("shoes.csv", Utf8(shoes)));

        var export = StravaExport.Read(zip);
        Assert.Equal("scott", Assert.Single(export.Activities).Gear);
        Assert.Equal(2, export.Gear.Count);
        Assert.Contains(export.Gear, g => g is { Nickname: "scott", Brand: "Scott", Model: "Speedster gravel 30 eq", Type: GearTypes.Bike });
        Assert.Contains(export.Gear, g => g is { Nickname: "hoka", Brand: "Hoka", Type: GearTypes.Shoes });
    }
}
