using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class FollowersTests(ApiFixture api)
{
    static async Task<int> IdAsync(ApiClient client) =>
        (await client.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

    static async Task<HttpResponseMessage> FollowAsync(ApiClient follower, int targetId) =>
        await follower.Http.PostAsync($"/api/v1/users/{targetId}/followers", null);

    static Task<HttpResponseMessage> AcceptAsync(ApiClient target, int followerId) =>
        target.Http.PostAsync($"/api/v1/follow-requests/{followerId}/accept", null);

    /// Each test follows a fresh target, so pending requests and follower counts never leak between tests.
    async Task<(ApiClient Target, int TargetId)> TargetAsync(ApiClient admin, string username)
    {
        await api.CreateUserAsync(admin, username);
        var target = await api.LoginAsync(username, "Str0ng-pass!");
        return (target, await IdAsync(target));
    }

    [Fact]
    public async Task Follow_request_is_pending_until_accepted_and_controls_network_access()
    {
        var admin = await api.LoginAsync();
        var (target, targetId) = await TargetAsync(admin, "target.one");
        await api.CreateUserAsync(admin, "follower.anna");
        var anna = await api.LoginAsync("follower.anna", "Str0ng-pass!");
        int annaId = await IdAsync(anna);
        await api.CreateUserAsync(admin, "follower.ben");
        var ben = await api.LoginAsync("follower.ben", "Str0ng-pass!");

        var follow = await FollowAsync(anna, targetId);
        Assert.Equal(HttpStatusCode.Created, follow.StatusCode);
        var pending = await follow.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal("pending", pending.GetProperty("status").GetString());
        Assert.Equal(annaId, pending.GetProperty("follower_id").GetInt32());
        Assert.Equal("follower.anna", pending.GetProperty("follower").GetProperty("name").GetString());

        var requests = await target.GetJsonAsync<JsonElement>("/api/v1/follow-requests");
        Assert.Equal(1, requests.GetProperty("total").GetInt32());
        Assert.Equal(annaId, requests.GetProperty("items")[0].GetProperty("follower_id").GetInt32());

        // Pending does not open the target's network yet, and a stranger never sees it.
        Assert.Equal(HttpStatusCode.NotFound, (await anna.Http.GetAsync($"/api/v1/users/{targetId}/followers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ben.Http.GetAsync($"/api/v1/users/{targetId}/followers")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(target, annaId)).StatusCode);

        var followers = await anna.GetJsonAsync<JsonElement>($"/api/v1/users/{targetId}/followers");
        Assert.Equal(1, followers.GetProperty("total").GetInt32());
        Assert.Equal(annaId, followers.GetProperty("items")[0].GetProperty("follower_id").GetInt32());

        var outgoing = await anna.GetJsonAsync<JsonElement>($"/api/v1/users/{targetId}/relationship");
        Assert.Equal("accepted", outgoing.GetProperty("outgoing").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, outgoing.GetProperty("incoming").ValueKind);

        var incoming = await target.GetJsonAsync<JsonElement>($"/api/v1/users/{annaId}/relationship");
        Assert.Equal(JsonValueKind.Null, incoming.GetProperty("outgoing").ValueKind);
        Assert.Equal("accepted", incoming.GetProperty("incoming").GetProperty("status").GetString());

        var profile = await anna.GetJsonAsync<JsonElement>($"/api/v1/people/{targetId}");
        Assert.Equal(1, profile.GetProperty("followers").GetInt32());
        Assert.Equal("accepted", profile.GetProperty("outgoing").GetProperty("status").GetString());

        // Unfollowing is the follower removing their own row.
        Assert.Equal(HttpStatusCode.NoContent, (await anna.Http.DeleteAsync($"/api/v1/users/{targetId}/followers/{annaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anna.Http.GetAsync($"/api/v1/users/{targetId}/followers")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await anna.GetJsonAsync<JsonElement>($"/api/v1/users/{targetId}/relationship")).GetProperty("outgoing").ValueKind);
    }

    [Fact]
    public async Task Self_follow_duplicates_and_missing_users_are_rejected()
    {
        var admin = await api.LoginAsync();
        var (target, targetId) = await TargetAsync(admin, "target.two");
        await api.CreateUserAsync(admin, "follower.cara");
        var cara = await api.LoginAsync("follower.cara", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest, (await FollowAsync(target, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FollowAsync(cara, 999_999)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await FollowAsync(cara, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FollowAsync(cara, targetId)).StatusCode);
    }

    [Fact]
    public async Task Rejecting_a_request_deletes_it_and_a_later_request_is_fresh()
    {
        var admin = await api.LoginAsync();
        var (target, targetId) = await TargetAsync(admin, "target.three");
        await api.CreateUserAsync(admin, "follower.dina");
        var dina = await api.LoginAsync("follower.dina", "Str0ng-pass!");
        int dinaId = await IdAsync(dina);

        Assert.Equal(HttpStatusCode.Created, (await FollowAsync(dina, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await target.Http.DeleteAsync($"/api/v1/follow-requests/{dinaId}")).StatusCode);
        Assert.Equal(0, (await target.GetJsonAsync<JsonElement>("/api/v1/follow-requests")).GetProperty("total").GetInt32());

        // No rejection tombstone: the same user may ask again.
        Assert.Equal(HttpStatusCode.Created, (await FollowAsync(dina, targetId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(target, dinaId)).StatusCode);

        // The followee may remove an accepted follower, and a third party may do neither.
        await api.CreateUserAsync(admin, "follower.ed");
        var ed = await api.LoginAsync("follower.ed", "Str0ng-pass!");
        Assert.Equal(HttpStatusCode.NotFound, (await ed.Http.DeleteAsync($"/api/v1/users/{targetId}/followers/{dinaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await target.Http.DeleteAsync($"/api/v1/users/{targetId}/followers/{dinaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dina.Http.GetAsync($"/api/v1/users/{targetId}/followers")).StatusCode);
    }

    [Fact]
    public async Task People_directory_exposes_only_public_identity_fields()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "follower.fay");
        var fay = await api.LoginAsync("follower.fay", "Str0ng-pass!");

        var page = await fay.GetJsonAsync<JsonElement>("/api/v1/people?q=Administrator");
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        var person = page.GetProperty("items")[0];
        Assert.Equal("Administrator", person.GetProperty("name").GetString());
        Assert.False(person.TryGetProperty("email", out _));
        Assert.False(person.TryGetProperty("access_type", out _));
        Assert.False(person.TryGetProperty("birthdate", out _));
        Assert.False(person.TryGetProperty("height", out _));
        Assert.False(person.TryGetProperty("max_heart_rate", out _));
        Assert.False(person.TryGetProperty("active", out _));
    }
}
