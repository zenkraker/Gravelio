using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class NotificationTests(ApiFixture api)
{
    static async Task<int> IdAsync(ApiClient client) =>
        (await client.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

    static async Task<JsonElement> NotificationsAsync(ApiClient client) =>
        await client.GetJsonAsync<JsonElement>("/api/v1/notifications?page_size=100");

    static async Task<int> UnreadAsync(ApiClient client) =>
        (await client.GetJsonAsync<JsonElement>("/api/v1/notifications/unread-count")).GetProperty("count").GetInt32();

    [Fact]
    public async Task Follow_request_and_acceptance_notify_the_other_party()
    {
        var admin = await api.LoginAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var targetName = $"notify.target.{suffix}";
        var requesterName = $"notify.requester.{suffix}";
        await api.CreateUserAsync(admin, targetName);
        await api.CreateUserAsync(admin, requesterName);
        var target = await api.LoginAsync(targetName, "Str0ng-pass!");
        var requester = await api.LoginAsync(requesterName, "Str0ng-pass!");
        int targetId = await IdAsync(target);
        int requesterId = await IdAsync(requester);

        Assert.Equal(HttpStatusCode.Created, (await requester.Http.PostAsync($"/api/v1/users/{targetId}/followers", null)).StatusCode);

        // The target is told who asked; the requester is not told about their own request.
        Assert.Equal(1, await UnreadAsync(target));
        var inbox = await NotificationsAsync(target);
        var request = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal(11, request.GetProperty("type").GetInt32());
        Assert.Equal(requesterId, request.GetProperty("actor_id").GetInt32());
        Assert.Equal(requesterName, request.GetProperty("actor_name").GetString());
        Assert.False(request.GetProperty("read").GetBoolean());
        Assert.Equal(0, await UnreadAsync(requester));

        Assert.Equal(HttpStatusCode.OK, (await target.Http.PostAsync($"/api/v1/follow-requests/{requesterId}/accept", null)).StatusCode);

        Assert.Equal(1, await UnreadAsync(requester));
        var accepted = Assert.Single((await NotificationsAsync(requester)).GetProperty("items").EnumerateArray());
        Assert.Equal(12, accepted.GetProperty("type").GetInt32());
        Assert.Equal(targetName, accepted.GetProperty("actor_name").GetString());
        // Accepting does not touch the request notification or create a second one.
        Assert.Equal(1, await UnreadAsync(target));
    }

    [Fact]
    public async Task Notifications_are_owned_marked_read_and_never_seen_by_others()
    {
        var admin = await api.LoginAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var targetName = $"notify.owner.{suffix}";
        await api.CreateUserAsync(admin, targetName);
        await api.CreateUserAsync(admin, $"notify.other.{suffix}");
        var target = await api.LoginAsync(targetName, "Str0ng-pass!");
        var other = await api.LoginAsync($"notify.other.{suffix}", "Str0ng-pass!");
        int targetId = await IdAsync(target);

        Assert.Equal(HttpStatusCode.Created, (await other.Http.PostAsync($"/api/v1/users/{targetId}/followers", null)).StatusCode);
        int id = (await NotificationsAsync(target)).GetProperty("items")[0].GetProperty("id").GetInt32();

        // Another user cannot mark it read, nor see it in their own inbox.
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PutAsync($"/api/v1/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(0, (await NotificationsAsync(other)).GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await target.Http.PutAsync($"/api/v1/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(0, await UnreadAsync(target));
        Assert.True((await NotificationsAsync(target)).GetProperty("items")[0].GetProperty("read").GetBoolean());
    }

    [Fact]
    public async Task Mark_all_as_read_only_clears_the_callers_notifications()
    {
        var admin = await api.LoginAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var targetName = $"notify.all.{suffix}";
        await api.CreateUserAsync(admin, targetName);
        var target = await api.LoginAsync(targetName, "Str0ng-pass!");
        int targetId = await IdAsync(target);

        foreach (var i in new[] { "a", "b" })
        {
            await api.CreateUserAsync(admin, $"notify.sender.{i}.{suffix}");
            var sender = await api.LoginAsync($"notify.sender.{i}.{suffix}", "Str0ng-pass!");
            Assert.Equal(HttpStatusCode.Created, (await sender.Http.PostAsync($"/api/v1/users/{targetId}/followers", null)).StatusCode);
        }
        Assert.Equal(2, await UnreadAsync(target));
        Assert.Equal(2, (await NotificationsAsync(target)).GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await target.Http.PutAsync("/api/v1/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, await UnreadAsync(target));
        var items = (await NotificationsAsync(target)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, n => Assert.True(n.GetProperty("read").GetBoolean()));
    }

    [Fact]
    public async Task Followers_hear_about_a_new_shared_activity_and_nobody_else_does()
    {
        var admin = await api.LoginAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var names = new[] { $"newact.owner.{suffix}", $"newact.fan.{suffix}", $"newact.pending.{suffix}", $"newact.stranger.{suffix}" };
        foreach (var name in names) await api.CreateUserAsync(admin, name);
        var owner = await api.LoginAsync(names[0], "Str0ng-pass!");
        var fan = await api.LoginAsync(names[1], "Str0ng-pass!");
        var pending = await api.LoginAsync(names[2], "Str0ng-pass!");
        var stranger = await api.LoginAsync(names[3], "Str0ng-pass!");
        int ownerId = await IdAsync(owner), fanId = await IdAsync(fan);

        Assert.Equal(HttpStatusCode.Created, (await fan.Http.PostAsync($"/api/v1/users/{ownerId}/followers", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PostAsync($"/api/v1/follow-requests/{fanId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await pending.Http.PostAsync($"/api/v1/users/{ownerId}/followers", null)).StatusCode); // never accepted
        var before = await UnreadAsync(fan);

        // A private activity is nobody's business.
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("a.gpx", TestData.Gpx("Quiet run", new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc))))).StatusCode);
        Assert.Equal(before, await UnreadAsync(fan));

        // Once the owner shares by default, the next upload is announced to accepted followers only.
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = 1 }, ApiClient.Json)).StatusCode);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("b.gpx", TestData.Gpx("Shared run", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc))));
        var activityId = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("id").GetInt32();

        Assert.Equal(before + 1, await UnreadAsync(fan));
        var news = (await NotificationsAsync(fan)).GetProperty("items").EnumerateArray().First(n => n.GetProperty("type").GetInt32() == 1);
        Assert.Equal(ownerId, news.GetProperty("actor_id").GetInt32());
        Assert.Equal(activityId, news.GetProperty("activity_id").GetInt32());
        Assert.Equal("Shared run", news.GetProperty("activity_name").GetString());
        Assert.DoesNotContain(1, (await NotificationsAsync(pending)).GetProperty("items").EnumerateArray().Select(n => n.GetProperty("type").GetInt32()));
        Assert.Equal(0, await UnreadAsync(stranger));
        Assert.Equal(0, (await NotificationsAsync(owner)).GetProperty("items").EnumerateArray().Count(n => n.GetProperty("type").GetInt32() == 1));

        // The same workout from another source is a hidden duplicate: the owner is told, the followers are not.
        var again = await owner.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("c.gpx", TestData.Gpx("Shared run again", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc), points: 59)));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(before + 1, await UnreadAsync(fan));
    }
}
