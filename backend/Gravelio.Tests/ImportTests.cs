using System.Globalization;
using System.Text;
using System.Text.Json;
using Dynastream.Fit;
using Gravelio.Api.Activities;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Data;
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
}
