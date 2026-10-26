using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class PublicLinksTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    static Task<HttpResponseMessage> SaveSettingsAsync(ApiClient admin, bool links, bool userInfo = false) =>
        admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { PublicShareableLinks = links, PublicShareableLinksUserInfo = userInfo }, ApiClient.Json);

    async Task<ApiClient> AnonymousAsync()
    {
        var client = new ApiClient(api.CreateClient());
        await client.SyncXsrfAsync();
        return client;
    }

    /// An owner with one uploaded activity (with heart rate) at the given visibility.
    async Task<(ApiClient Owner, int Id)> ActivityAsync(string username, int visibility, int day)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        var owner = await api.LoginAsync(username, Password);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload",
            TestData.Upload("link.gpx", TestData.Gpx("Linked run", new DateTime(2026, 7, day, 8, 0, 0, DateTimeKind.Utc), heartRate: true)));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        var update = await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Linked run", Description = "Shared on purpose", ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = visibility }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        return (owner, id);
    }

    [Fact]
    public async Task Links_are_off_until_an_admin_turns_them_on_and_then_only_public_activities_open()
    {
        var admin = await api.LoginAsync();
        var (owner, publicId) = await ActivityAsync("link.owner", Visibilities.Public, 1);
        var anon = await AnonymousAsync();

        try
        {
            Assert.False((await anon.GetJsonAsync<JsonElement>("/api/v1/server-settings/public")).GetProperty("public_shareable_links").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{publicId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{publicId}/streams")).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await SaveSettingsAsync(admin, links: true)).StatusCode);
            Assert.True((await anon.GetJsonAsync<JsonElement>("/api/v1/server-settings/public")).GetProperty("public_shareable_links").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await anon.Http.GetAsync($"/api/v1/public/activities/{publicId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await anon.Http.GetAsync($"/api/v1/public/activities/{publicId}/streams")).StatusCode);

            // Followers-only and private activities, and ids that do not exist, answer the same 404.
            foreach (var visibility in new[] { Visibilities.Followers, Visibilities.Private })
            {
                var (_, hiddenId) = await ActivityAsync($"link.hidden{visibility}", visibility, 2 + visibility);
                Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{hiddenId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{hiddenId}/streams")).StatusCode);
            }
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync("/api/v1/public/activities/987654")).StatusCode);
        }
        finally
        {
            await SaveSettingsAsync(admin, links: false);
        }
    }

    [Fact]
    public async Task A_public_link_shows_only_a_minimal_activity_and_the_owner_name_only_when_allowed()
    {
        var admin = await api.LoginAsync();
        var (_, id) = await ActivityAsync("link.minimal", Visibilities.Public, 10);
        var anon = await AnonymousAsync();

        try
        {
            await SaveSettingsAsync(admin, links: true);
            var body = await anon.GetJsonAsync<JsonElement>($"/api/v1/public/activities/{id}");
            Assert.Equal("Linked run", body.GetProperty("name").GetString());
            Assert.Equal("Shared on purpose", body.GetProperty("description").GetString());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("owner_name").ValueKind);

            // The sensitive and internal fields are not merely empty: they are not in the response at all.
            var keys = body.EnumerateObject().Select(p => p.Name).ToHashSet();
            foreach (var forbidden in new[] { "owner", "user_id", "username", "email", "birthdate", "visibility", "gear_id", "created_at", "end_time", "tracker_manufacturer", "tracker_model", "calories", "height", "max_heart_rate", "role", "access_type", "active" })
                Assert.DoesNotContain(forbidden, keys);

            await SaveSettingsAsync(admin, links: true, userInfo: true);
            Assert.Equal("link.minimal", (await anon.GetJsonAsync<JsonElement>($"/api/v1/public/activities/{id}")).GetProperty("owner_name").GetString());
        }
        finally
        {
            await SaveSettingsAsync(admin, links: false);
        }
    }

    [Fact]
    public async Task What_the_owner_hides_is_hidden_on_a_public_link_too()
    {
        var admin = await api.LoginAsync();
        var (owner, id) = await ActivityAsync("link.private.parts", Visibilities.Public, 12);
        var anon = await AnonymousAsync();

        try
        {
            await SaveSettingsAsync(admin, links: true);
            var open = await anon.GetJsonAsync<JsonElement>($"/api/v1/public/activities/{id}");
            Assert.NotEqual(JsonValueKind.Null, open.GetProperty("average_hr").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, open.GetProperty("start_time").ValueKind);
            var openTypes = (await anon.GetJsonAsync<JsonElement[]>($"/api/v1/public/activities/{id}/streams")).Select(s => s.GetProperty("stream_type").GetInt32()).ToHashSet();
            Assert.Contains(1, openTypes); // heart rate
            Assert.Contains(7, openTypes); // map

            var hide = await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy",
                new { DefaultActivityVisibility = Visibilities.Public, HideActivityStartTime = true, HideActivityMap = true, HideActivityHr = true }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.OK, hide.StatusCode);

            var masked = await anon.GetJsonAsync<JsonElement>($"/api/v1/public/activities/{id}");
            Assert.Equal(JsonValueKind.Null, masked.GetProperty("average_hr").ValueKind);
            Assert.Equal(JsonValueKind.Null, masked.GetProperty("max_hr").ValueKind);
            Assert.Equal(JsonValueKind.Null, masked.GetProperty("start_time").ValueKind);
            var maskedStreams = await anon.GetJsonAsync<JsonElement[]>($"/api/v1/public/activities/{id}/streams");
            var maskedTypes = maskedStreams.Select(s => s.GetProperty("stream_type").GetInt32()).ToHashSet();
            Assert.DoesNotContain(1, maskedTypes);
            Assert.DoesNotContain(7, maskedTypes);
            // With the start time hidden, what is left carries offsets from a fixed epoch, not the real clock time.
            Assert.All(maskedStreams.SelectMany(s => s.GetProperty("waypoints").EnumerateArray()),
                w => Assert.StartsWith("1970-01-01", w.GetProperty("time").GetString()));
        }
        finally
        {
            await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = Visibilities.Private }, ApiClient.Json);
            await SaveSettingsAsync(admin, links: false);
        }
    }

    [Fact]
    public async Task Activity_type_names_are_readable_without_signing_in_for_the_share_page()
    {
        var anon = await AnonymousAsync();
        var names = await anon.GetJsonAsync<JsonElement>("/api/v1/activities/types");
        Assert.Equal("Run", names.GetProperty("1").GetString());
    }

    [Fact]
    public async Task A_deactivated_owners_public_activity_stops_opening()
    {
        var admin = await api.LoginAsync();
        var (owner, id) = await ActivityAsync("link.gone", Visibilities.Public, 14);
        var ownerId = (await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();
        var anon = await AnonymousAsync();

        try
        {
            await SaveSettingsAsync(admin, links: true);
            Assert.Equal(HttpStatusCode.OK, (await anon.Http.GetAsync($"/api/v1/public/activities/{id}")).StatusCode);
            var off = await admin.Http.PutAsJsonAsync($"/api/v1/users/{ownerId}",
                new { Name = "link.gone", Username = "link.gone", Email = "link.gone@example.com", AccessType = "regular", Active = false }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.Http.GetAsync($"/api/v1/public/activities/{id}")).StatusCode);
        }
        finally
        {
            await SaveSettingsAsync(admin, links: false);
        }
    }
}
