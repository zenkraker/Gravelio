using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

[Collection("api")]
public class ApiKeyTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    async Task<ApiClient> UserAsync(string username)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        return await api.LoginAsync(username, Password);
    }

    static async Task<(int Id, string Key)> CreateKeyAsync(ApiClient client, string name = "Watch sync", int? expiresInDays = null)
    {
        var response = await client.Http.PostAsJsonAsync("/api/v1/users/me/api-keys",
            new { Name = name, ExpiresInDays = expiresInDays, CurrentPassword = Password }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        return (body.GetProperty("id").GetInt32(), body.GetProperty("key").GetString()!);
    }

    /// Uploads with only the key: no cookie, no antiforgery token.
    async Task<HttpResponseMessage> UploadWithKeyAsync(string? key, DateTime start, string path = "/api/v1/activities/upload")
    {
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = TestData.Upload("sync.gpx", TestData.Gpx("Synced run", start)),
        };
        if (key is not null) request.Headers.Add("X-API-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task A_key_is_shown_once_and_only_its_prefix_is_listed()
    {
        var user = await UserAsync("keys.once");
        var (id, key) = await CreateKeyAsync(user);

        Assert.StartsWith("gravelio_", key);
        Assert.True(key.Length > 40);
        var listed = await user.Http.GetStringAsync("/api/v1/users/me/api-keys");
        Assert.DoesNotContain(key, listed);
        var item = JsonSerializer.Deserialize<JsonElement[]>(listed, ApiClient.Json)!.Single();
        Assert.Equal(id, item.GetProperty("id").GetInt32());
        Assert.Equal(key[..13], item.GetProperty("prefix").GetString());
        Assert.DoesNotContain("key_hash", listed);
    }

    [Fact]
    public async Task Making_a_key_needs_the_current_password()
    {
        var user = await UserAsync("keys.step");
        var wrong = await user.Http.PostAsJsonAsync("/api/v1/users/me/api-keys", new { Name = "x", CurrentPassword = "nope" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Empty(await user.GetJsonAsync<JsonElement[]>("/api/v1/users/me/api-keys"));
    }

    [Fact]
    public async Task A_key_uploads_for_its_owner_without_a_session_and_nothing_else()
    {
        var user = await UserAsync("keys.upload");
        var (_, key) = await CreateKeyAsync(user);

        var upload = await UploadWithKeyAsync(key, new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var activities = await user.GetJsonAsync<JsonElement>("/api/v1/activities");
        Assert.Equal(1, activities.GetProperty("total").GetInt32());
        Assert.Equal("Synced run", activities.GetProperty("items")[0].GetProperty("name").GetString());

        // The key opens the upload route and no other: it is not a login.
        using var client = api.CreateClient();
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        me.Headers.Add("X-API-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(me)).StatusCode);
        using var gears = new HttpRequestMessage(HttpMethod.Post, "/api/v1/gears") { Content = JsonContent.Create(new { Nickname = "x", GearType = 1 }, options: ApiClient.Json) };
        gears.Headers.Add("X-API-Key", key);
        Assert.NotEqual(HttpStatusCode.Created, (await client.SendAsync(gears)).StatusCode);
    }

    [Fact]
    public async Task A_missing_wrong_revoked_expired_or_query_string_key_is_refused()
    {
        var user = await UserAsync("keys.refused");
        var (revokedId, revoked) = await CreateKeyAsync(user, "Old");
        var (expiredId, expired) = await CreateKeyAsync(user, "Short", expiresInDays: 1);
        var (_, good) = await CreateKeyAsync(user, "Good");

        // With no key header the request is treated like any browser POST and fails the antiforgery check first.
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadWithKeyAsync(null, new DateTime(2026, 2, 2, 8, 0, 0, DateTimeKind.Utc))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadWithKeyAsync("gravelio_" + new string('a', 43), new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc))).StatusCode);
        // The key is accepted in the header only: in a URL it would be written to logs.
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadWithKeyAsync(null, new DateTime(2026, 2, 4, 8, 0, 0, DateTimeKind.Utc), $"/api/v1/activities/upload?api_key={good}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PostAsync($"/api/v1/users/me/api-keys/{revokedId}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadWithKeyAsync(revoked, new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc))).StatusCode);

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.ApiKeys.Where(k => k.Id == expiredId).ExecuteUpdateAsync(s => s.SetProperty(k => k.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadWithKeyAsync(expired, new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await UploadWithKeyAsync(good, new DateTime(2026, 2, 7, 8, 0, 0, DateTimeKind.Utc))).StatusCode);
        var listed = await user.GetJsonAsync<JsonElement[]>("/api/v1/users/me/api-keys");
        Assert.NotEqual(JsonValueKind.Null, listed.Single(k => k.GetProperty("name").GetString() == "Old").GetProperty("revoked_at").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, listed.Single(k => k.GetProperty("name").GetString() == "Good").GetProperty("last_used_at").ValueKind);
    }

    [Fact]
    public async Task A_deactivated_users_key_stops_working()
    {
        var admin = await api.LoginAsync();
        var user = await UserAsync("keys.gone");
        var (_, key) = await CreateKeyAsync(user);
        var id = (await user.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await UploadWithKeyAsync(key, new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc))).StatusCode);

        var off = await admin.Http.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "keys.gone", Username = "keys.gone", Email = "keys.gone@example.com", AccessType = "regular", Active = false }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadWithKeyAsync(key, new DateTime(2026, 2, 11, 8, 0, 0, DateTimeKind.Utc))).StatusCode);
    }

    [Fact]
    public async Task Keys_belong_to_their_owner()
    {
        var owner = await UserAsync("keys.owner");
        var other = await UserAsync("keys.other");
        var (id, key) = await CreateKeyAsync(owner);

        Assert.Empty(await other.GetJsonAsync<JsonElement[]>("/api/v1/users/me/api-keys"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PostAsync($"/api/v1/users/me/api-keys/{id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/users/me/api-keys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadWithKeyAsync(key, new DateTime(2026, 2, 12, 8, 0, 0, DateTimeKind.Utc))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.DeleteAsync($"/api/v1/users/me/api-keys/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadWithKeyAsync(key, new DateTime(2026, 2, 13, 8, 0, 0, DateTimeKind.Utc))).StatusCode);
    }

    [Fact]
    public async Task A_browser_session_cannot_skip_the_antiforgery_check_by_adding_the_header()
    {
        var user = await UserAsync("keys.csrf");
        var (_, key) = await CreateKeyAsync(user);
        // This client holds the session cookie but sends no antiforgery token.
        user.Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/activities/upload")
        {
            Content = TestData.Upload("csrf.gpx", TestData.Gpx("Forged", new DateTime(2026, 2, 14, 8, 0, 0, DateTimeKind.Utc))),
        };
        request.Headers.Add("X-API-Key", key);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task A_user_can_hold_only_a_limited_number_of_keys()
    {
        var user = await UserAsync("keys.limit");
        for (var i = 0; i < 20; i++) await CreateKeyAsync(user, $"Key {i}");
        var response = await user.Http.PostAsJsonAsync("/api/v1/users/me/api-keys", new { Name = "One too many", CurrentPassword = Password }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
