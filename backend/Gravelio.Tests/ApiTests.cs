using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class ApiTests(ApiFixture api)
{
    static string Gpx(string name, DateTime start, int points = 60)
    {
        var pts = string.Concat(Enumerable.Range(0, points).Select(i =>
            $"<trkpt lat=\"{38.7223 + i * 0.0005}\" lon=\"{-9.1393 + i * 0.0003}\"><ele>{100 + i}</ele><time>{start.AddSeconds(i * 10):yyyy-MM-ddTHH:mm:ssZ}</time></trkpt>"));
        return $"<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><name>{name}</name><type>running</type><trkseg>{pts}</trkseg></trk></gpx>";
    }

    static MultipartFormDataContent Upload(string fileName, string content)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Anonymous_requests_get_401_not_a_login_redirect()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unsafe_request_without_xsrf_token_is_rejected()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Username = "admin", Password = ApiFixture.AdminPassword }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_logs_in_and_reads_profile_with_role()
    {
        var admin = await api.LoginAsync();
        var me = await admin.GetJsonAsync<JsonElement>("/api/v1/users/me");
        Assert.Equal("admin", me.GetProperty("username").GetString());
        Assert.Equal("admin", me.GetProperty("access_type").GetString());
    }

    [Fact]
    public async Task The_fifth_wrong_password_locks_the_account()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "locky");
        var client = new ApiClient(api.CreateClient());
        await client.SyncXsrfAsync();
        for (int i = 0; i < 4; i++)
        {
            var wrong = await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = "locky", Password = "wrong" }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        var fifth = await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = "locky", Password = "wrong" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        var locked = await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = "locky", Password = "Str0ng-pass!" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }

    [Fact]
    public async Task Weak_passwords_are_rejected_and_admins_need_twelve_characters()
    {
        var admin = await api.LoginAsync();
        var weak = await admin.Http.PostAsJsonAsync("/api/v1/users",
            new { Name = "w", Username = "weakling", Email = "weak@example.com", AccessType = "regular", Active = true, Password = "password" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var shortAdmin = await admin.Http.PostAsJsonAsync("/api/v1/users",
            new { Name = "s", Username = "shortadmin", Email = "sa@example.com", AccessType = "admin", Active = true, Password = "Short-1!x" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, shortAdmin.StatusCode);
    }

    [Fact]
    public async Task Gpx_upload_is_parsed_listed_streamed_deduplicated_and_deleted()
    {
        var admin = await api.LoginAsync();
        var gpx = Gpx("Lisbon run", new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc));

        var upload = await admin.Http.PostAsync("/api/v1/activities/upload", Upload("run.gpx", gpx));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var created = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        int id = created.GetProperty("id").GetInt32();
        Assert.Equal("Lisbon run", created.GetProperty("name").GetString());
        Assert.Equal(3618, created.GetProperty("distance").GetInt32());
        Assert.Equal("Europe/Lisbon", created.GetProperty("timezone").GetString());

        var streams = await admin.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/streams");
        Assert.Contains(streams, s => s.GetProperty("stream_type").GetInt32() == 7 && s.GetProperty("waypoints").GetArrayLength() == 60);

        var page = await admin.GetJsonAsync<JsonElement>("/api/v1/activities?page=1&page_size=5");
        Assert.True(page.GetProperty("total").GetInt32() >= 1);

        var duplicate = await admin.Http.PostAsync("/api/v1/activities/upload", Upload("run.gpx", gpx));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var rename = await admin.Http.PutAsJsonAsync($"/api/v1/activities/{id}", new { Name = "Renamed", ActivityType = 1, Visibility = 2 }, ApiClient.Json);
        Assert.Equal("Renamed", (await rename.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"/api/v1/activities/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Http.GetAsync($"/api/v1/activities/{id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_uploads_are_rejected()
    {
        var admin = await api.LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Http.PostAsync("/api/v1/activities/upload", Upload("notes.txt", "hi"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.Http.PostAsync("/api/v1/activities/upload", Upload("bad.gpx", "<gpx/>"))).StatusCode);
    }

    [Fact]
    public async Task Users_only_see_their_own_activities_and_cannot_use_admin_routes()
    {
        var admin = await api.LoginAsync();
        var upload = await admin.Http.PostAsync("/api/v1/activities/upload", Upload("private.gpx", Gpx("Private", new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc))));
        int adminActivity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("id").GetInt32();
        await api.CreateUserAsync(admin, "ann");

        var ann = await api.LoginAsync("ann", "Str0ng-pass!");
        Assert.Equal(HttpStatusCode.NotFound, (await ann.Http.GetAsync($"/api/v1/activities/{adminActivity}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ann.Http.DeleteAsync($"/api/v1/activities/{adminActivity}")).StatusCode);
        Assert.Equal(0, (await ann.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await ann.Http.GetAsync("/api/v1/users")).StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_user_ends_their_session_immediately()
    {
        var admin = await api.LoginAsync();
        int id = await api.CreateUserAsync(admin, "bob");
        var bob = await api.LoginAsync("bob", "Str0ng-pass!");
        Assert.Equal(HttpStatusCode.OK, (await bob.Http.GetAsync("/api/v1/users/me")).StatusCode);

        var update = await admin.Http.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "bob", Username = "bob", Email = "bob@example.com", AccessType = "regular", Active = false }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await bob.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task Changing_password_keeps_this_session_and_ends_others()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "cara");
        var phone = await api.LoginAsync("cara", "Str0ng-pass!");
        var laptop = await api.LoginAsync("cara", "Str0ng-pass!");

        var change = await laptop.Http.PutAsJsonAsync("/api/v1/users/me/password", new { CurrentPassword = "Str0ng-pass!", NewPassword = "N3w-passw0rd!" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var admin = await api.LoginAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }
}
