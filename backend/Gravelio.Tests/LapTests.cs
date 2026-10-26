using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dynastream.Fit;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using DateTime = System.DateTime;

namespace Gravelio.Tests;

[Collection("api")]
public class LapTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";
    static readonly DateTime Start = new(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc);

    /// Two laps of 30 points each, 10 s apart, with lap totals as a Garmin writes them.
    static string Tcx(DateTime start)
    {
        string Lap(int lap) => string.Concat(Enumerable.Range(lap * 30, 30).Select(i => string.Create(CultureInfo.InvariantCulture,
            $"<Trackpoint><Time>{start.AddSeconds(i * 10):yyyy-MM-ddTHH:mm:ssZ}</Time><Position><LatitudeDegrees>{38.7223 + i * 0.0005}</LatitudeDegrees><LongitudeDegrees>{-9.1393 + i * 0.0003}</LongitudeDegrees></Position><AltitudeMeters>{100 + i}</AltitudeMeters><HeartRateBpm><Value>{130 + i % 15}</Value></HeartRateBpm></Trackpoint>")));
        string LapXml(int lap) => string.Create(CultureInfo.InvariantCulture,
            $"<Lap StartTime=\"{start.AddSeconds(lap * 300):yyyy-MM-ddTHH:mm:ssZ}\"><TotalTimeSeconds>300</TotalTimeSeconds><DistanceMeters>1800</DistanceMeters><MaximumSpeed>6.5</MaximumSpeed><Calories>120</Calories><AverageHeartRateBpm><Value>{140 + lap}</Value></AverageHeartRateBpm><MaximumHeartRateBpm><Value>{150 + lap}</Value></MaximumHeartRateBpm><Track>{Lap(lap)}</Track></Lap>");
        return $"<TrainingCenterDatabase xmlns=\"http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2\"><Activities><Activity Sport=\"Running\">{LapXml(0)}{LapXml(1)}</Activity></Activities></TrainingCenterDatabase>";
    }

    [Fact]
    public void A_tcx_file_yields_its_laps_with_their_first_and_last_fix()
    {
        var parsed = ActivityFileParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Tcx(Start))), ".tcx").Single();
        Assert.Equal(2, parsed.Laps.Count);
        var second = parsed.Laps[1];
        Assert.Equal(Start.AddSeconds(300), second.StartTime);
        Assert.Equal(300, second.TotalElapsedTime);
        Assert.Equal(1800, second.Distance);
        Assert.Equal(141, second.AvgHr);
        Assert.Equal(151, second.MaxHr);
        Assert.Equal(38.7223 + 30 * 0.0005, second.StartLat!.Value, 6);
        Assert.Equal(38.7223 + 59 * 0.0005, second.EndLat!.Value, 6);
    }

    [Fact]
    public void A_fit_file_assigns_each_lap_to_its_session()
    {
        const double toSemicircles = 2147483648.0 / 180.0;
        var ms = new MemoryStream();
        var enc = new Encode(ProtocolVersion.V20);
        enc.Open(ms);
        var fileId = new FileIdMesg();
        fileId.SetType(Dynastream.Fit.File.Activity);
        fileId.SetManufacturer(Manufacturer.Garmin);
        enc.Write(fileId);
        for (int i = 0; i < 60; i++)
        {
            var r = new RecordMesg();
            r.SetTimestamp(new Dynastream.Fit.DateTime(Start.AddSeconds(i * 10)));
            r.SetPositionLat((int)((38.7223 + i * 0.0005) * toSemicircles));
            r.SetPositionLong((int)((-9.1393 + i * 0.0003) * toSemicircles));
            enc.Write(r);
        }
        foreach (var lap in new[] { 0, 1 })
        {
            var l = new LapMesg();
            l.SetStartTime(new Dynastream.Fit.DateTime(Start.AddSeconds(lap * 300)));
            l.SetTimestamp(new Dynastream.Fit.DateTime(Start.AddSeconds(lap * 300 + 300)));
            l.SetStartPositionLat((int)((38.7223 + lap * 30 * 0.0005) * toSemicircles));
            l.SetStartPositionLong((int)(-9.1393 * toSemicircles));
            l.SetTotalElapsedTime(300);
            l.SetTotalDistance(1800);
            l.SetAvgHeartRate((byte)(140 + lap));
            l.SetTotalAscent(12);
            enc.Write(l);
        }
        var s = new SessionMesg();
        s.SetStartTime(new Dynastream.Fit.DateTime(Start));
        s.SetTimestamp(new Dynastream.Fit.DateTime(Start.AddSeconds(590)));
        s.SetTotalElapsedTime(590);
        s.SetSport(Sport.Running);
        enc.Write(s);
        enc.Close();

        var parsed = ActivityFileParser.Parse(new MemoryStream(ms.ToArray()), ".fit").Single();
        Assert.Equal(2, parsed.Laps.Count);
        Assert.Equal(141, parsed.Laps[1].AvgHr);
        Assert.Equal(12, parsed.Laps[0].ElevationGain);
        Assert.Equal(38.7223 + 30 * 0.0005, parsed.Laps[1].StartLat!.Value, 5);
    }

    async Task<(ApiClient Owner, int Id)> UploadAsync(string username, int visibility)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        var owner = await api.LoginAsync(username, Password);
        var start = Start.AddDays(Math.Abs(username.GetHashCode()) % 300);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("laps.tcx", Tcx(start)));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Laps", ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = visibility }, ApiClient.Json)).StatusCode);
        return (owner, id);
    }

    [Fact]
    public async Task The_owner_reads_the_laps_and_a_stranger_cannot()
    {
        var (owner, id) = await UploadAsync("laps.owner1", Visibilities.Private);
        var laps = await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/laps");
        Assert.Equal([1, 2], laps.Select(l => l.GetProperty("index").GetInt32()));
        Assert.Equal(300, laps[1].GetProperty("start_offset").GetDouble());
        Assert.Equal(6, laps[1].GetProperty("average_speed").GetDouble()); // 1800 m in 300 s, worked out because the file gave none
        Assert.Equal(141, laps[1].GetProperty("average_hr").GetInt32());

        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "laps.stranger", Password);
        var stranger = await api.LoginAsync("laps.stranger", Password);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{id}/laps")).StatusCode);
    }

    // Every hide flag and the lap fields it must blank. A lap field no flag names would have to be added here
    // and in VisibleLaps, so nothing the owner hid can stay visible by omission.
    public static TheoryData<string, string[]> Hidden => new()
    {
        { "HideActivityMap", ["start_lat", "start_lon", "end_lat", "end_lon"] },
        { "HideActivityLocation", ["start_lat", "start_lon", "end_lat", "end_lon"] },
        { "HideActivityStartTime", ["start_time"] },
        { "HideActivityHr", ["average_hr", "max_hr"] },
        { "HideActivityCadence", ["average_cad", "max_cad"] },
        { "HideActivityPower", ["average_power", "max_power"] },
        { "HideActivityElevation", ["elevation_gain", "elevation_loss"] },
        { "HideActivitySpeed", ["average_speed", "max_speed"] },
    };

    static readonly string[] Maskable =
        ["start_time", "start_lat", "start_lon", "end_lat", "end_lon", "average_hr", "max_hr", "average_cad", "max_cad", "average_power", "max_power",
         "elevation_gain", "elevation_loss", "average_speed", "max_speed"];

    [Theory]
    [MemberData(nameof(Hidden))]
    public async Task Hiding_something_hides_the_lap_fields_derived_from_it(string flag, string[] blanked)
    {
        var admin = await api.LoginAsync();
        var (owner, id) = await UploadAsync($"laps.{flag}".ToLowerInvariant(), Visibilities.Public);
        using (var scope = api.Services.CreateScope())
        {
            // Give the lap every field, so each flag has something real to hide.
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            foreach (var lap in db.ActivityLaps.Where(l => l.ActivityId == id))
            {
                lap.StartLat = 38.7; lap.StartLon = -9.1; lap.EndLat = 38.8; lap.EndLon = -9.2;
                lap.AverageHr = 140; lap.MaxHr = 150; lap.AverageCad = 80; lap.MaxCad = 90; lap.AveragePower = 200; lap.MaxPower = 300;
                lap.ElevationGain = 10; lap.ElevationLoss = 8; lap.AverageSpeed = 3; lap.MaxSpeed = 5;
            }
            await db.SaveChangesAsync();
        }
        var anon = new ApiClient(api.CreateClient());
        await anon.SyncXsrfAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { PublicShareableLinks = true }, ApiClient.Json)).StatusCode);
            var all = await anon.GetJsonAsync<JsonElement[]>($"/api/v1/public/activities/{id}/laps");
            Assert.All(all, l => Assert.All(Maskable, f => Assert.NotEqual(JsonValueKind.Null, l.GetProperty(f).ValueKind)));

            var privacy = new Dictionary<string, object> { ["DefaultActivityVisibility"] = Visibilities.Public, [flag] = true };
            Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", privacy, ApiClient.Json)).StatusCode);
            var masked = await anon.GetJsonAsync<JsonElement[]>($"/api/v1/public/activities/{id}/laps");
            Assert.All(masked, l =>
            {
                foreach (var field in Maskable)
                    Assert.Equal(blanked.Contains(field), l.GetProperty(field).ValueKind == JsonValueKind.Null);
                // The order and spacing stay, so a table still reads.
                Assert.NotEqual(JsonValueKind.Null, l.GetProperty("start_offset").ValueKind);
            });
            // The owner still sees everything.
            var own = await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/laps");
            Assert.All(own, l => Assert.All(Maskable, f => Assert.NotEqual(JsonValueKind.Null, l.GetProperty(f).ValueKind)));
        }
        finally
        {
            await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = Visibilities.Private }, ApiClient.Json);
            await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { PublicShareableLinks = false }, ApiClient.Json);
        }
    }

    [Fact]
    public async Task Public_laps_answer_404_unless_the_activity_is_publicly_linkable()
    {
        var admin = await api.LoginAsync();
        var (_, id) = await UploadAsync("laps.private1", Visibilities.Private);
        var anon = new ApiClient(api.CreateClient());
        await anon.SyncXsrfAsync();
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{id}/laps")).StatusCode);
            await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { PublicShareableLinks = true }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{id}/laps")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync("/api/v1/public/activities/987654/laps")).StatusCode);
        }
        finally
        {
            await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { PublicShareableLinks = false }, ApiClient.Json);
        }
    }

    [Fact]
    public async Task Laps_survive_an_export_and_an_import_into_another_account()
    {
        var (owner, id) = await UploadAsync("laps.exporter", Visibilities.Private);
        var original = await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/laps");

        var zip = await (await owner.Http.GetAsync("/api/v1/users/me/export")).Content.ReadAsByteArrayAsync();
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "laps.importer", Password);
        var importer = await api.LoginAsync("laps.importer", Password);
        var file = new ByteArrayContent(zip);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        Assert.Equal(HttpStatusCode.OK, (await importer.Http.PostAsync("/api/v1/users/me/import", new MultipartFormDataContent { { file, "file", "export.zip" } })).StatusCode);

        var imported = (await importer.GetJsonAsync<JsonElement>("/api/v1/activities?page_size=10")).GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32();
        var copy = await importer.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{imported}/laps");
        Assert.Equal(original.Length, copy.Length);
        Assert.Equal(original.Select(l => l.ToString()), copy.Select(l => l.ToString()));
    }
}
