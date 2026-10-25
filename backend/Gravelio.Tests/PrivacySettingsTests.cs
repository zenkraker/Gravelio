using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class PrivacySettingsTests(ApiFixture api)
{
    static async Task<int> IdAsync(ApiClient client) =>
        (await client.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

    static string GpxWithHr(string name, DateTime start)
    {
        var points = string.Concat(Enumerable.Range(0, 60).Select(i => string.Create(CultureInfo.InvariantCulture,
            $"<trkpt lat=\"{38.7223 + i * 0.0005}\" lon=\"{-9.1393 + i * 0.0003}\"><ele>{100 + i}</ele><time>{start.AddSeconds(i * 10):yyyy-MM-ddTHH:mm:ssZ}</time><extensions><hr>{120 + i % 10}</hr></extensions></trkpt>")));
        return $"<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><name>{name}</name><type>running</type><trkseg>{points}</trkseg></trk></gpx>";
    }

    [Fact]
    public async Task Hidden_fields_and_streams_are_masked_for_followers_but_not_the_owner()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "privacy.owner");
        await api.CreateUserAsync(admin, "privacy.fan");
        await api.CreateUserAsync(admin, "privacy.stranger");
        var owner = await api.LoginAsync("privacy.owner", "Str0ng-pass!");
        var fan = await api.LoginAsync("privacy.fan", "Str0ng-pass!");
        var stranger = await api.LoginAsync("privacy.stranger", "Str0ng-pass!");
        int ownerId = await IdAsync(owner), fanId = await IdAsync(fan);

        var put = await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new
        {
            DefaultActivityVisibility = Visibilities.Followers,
            HideActivityStartTime = true,
            HideActivityLocation = true,
            HideActivityMap = true,
            HideActivityHr = true,
            HideActivityGear = true,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(Visibilities.Followers, saved.GetProperty("default_activity_visibility").GetInt32());
        Assert.True(saved.GetProperty("hide_activity_hr").GetBoolean());
        Assert.False(saved.GetProperty("hide_activity_power").GetBoolean());

        var got = await owner.GetJsonAsync<JsonElement>("/api/v1/users/me/privacy");
        Assert.True(got.GetProperty("hide_activity_map").GetBoolean());
        Assert.False(got.GetProperty("hide_activity_pace").GetBoolean());

        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload",
            TestData.Upload("privacy.gpx", GpxWithHr("Privacy ride", start)));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        int activityId = uploaded.GetProperty("id").GetInt32();
        // The owner's default visibility is applied to a new upload.
        Assert.Equal(Visibilities.Followers, uploaded.GetProperty("visibility").GetInt32());
        Assert.Equal(JsonValueKind.String, uploaded.GetProperty("start_time").ValueKind);
        Assert.True(uploaded.GetProperty("average_hr").GetInt32() > 0);

        Assert.Equal(HttpStatusCode.Created, (await fan.Http.PostAsync($"/api/v1/users/{ownerId}/followers", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PostAsync($"/api/v1/follow-requests/{fanId}/accept", null)).StatusCode);

        var shared = await fan.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}");
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("start_time").ValueKind);
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("end_time").ValueKind);
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("average_hr").ValueKind);
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("max_hr").ValueKind);
        Assert.Equal(JsonValueKind.Null, shared.GetProperty("gear_id").ValueKind);

        var streams = await fan.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}/streams");
        var types = streams.EnumerateArray().Select(s => s.GetProperty("stream_type").GetInt32()).ToList();
        Assert.DoesNotContain(StreamTypes.Hr, types);
        Assert.DoesNotContain(StreamTypes.Map, types);
        Assert.DoesNotContain(StreamTypes.Temperature, types);
        var elevation = streams.EnumerateArray().First(s => s.GetProperty("stream_type").GetInt32() == StreamTypes.Elevation);
        var times = elevation.GetProperty("waypoints").EnumerateArray().Select(w => w.GetProperty("time").GetString()).ToList();
        Assert.All(times, t => Assert.StartsWith("1970-01-01", t));

        var list = await fan.GetJsonAsync<JsonElement>($"/api/v1/users/{ownerId}/activities");
        var item = list.GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, item.GetProperty("start_time").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("average_hr").ValueKind);
        var feed = await fan.GetJsonAsync<JsonElement>("/api/v1/activities/feed");
        Assert.Equal(1, feed.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("items")[0].GetProperty("average_hr").ValueKind);

        var own = await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}");
        Assert.Equal(JsonValueKind.String, own.GetProperty("start_time").ValueKind);
        Assert.True(own.GetProperty("average_hr").GetInt32() > 0);
        var ownStreams = await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}/streams");
        var ownTypes = ownStreams.EnumerateArray().Select(s => s.GetProperty("stream_type").GetInt32()).ToList();
        Assert.Contains(StreamTypes.Hr, ownTypes);
        Assert.Contains(StreamTypes.Map, ownTypes);
        var ownHr = ownStreams.EnumerateArray().First(s => s.GetProperty("stream_type").GetInt32() == StreamTypes.Hr);
        Assert.StartsWith("2026-09-01", ownHr.GetProperty("waypoints")[0].GetProperty("time").GetString());

        // Followers-only stays hidden from a stranger.
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{activityId}")).StatusCode);
    }

    [Fact]
    public async Task Privacy_settings_reject_an_unknown_visibility()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "privacy.invalid");
        var user = await api.LoginAsync("privacy.invalid", "Str0ng-pass!");
        var response = await user.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = 5 }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
