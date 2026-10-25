using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class ActivitySharingTests(ApiFixture api)
{
    static async Task<int> IdAsync(ApiClient client) =>
        (await client.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

    static async Task<(int Id, string Name, int Type)> UploadAsync(ApiClient client, string name, int day)
    {
        var response = await client.Http.PostAsync("/api/v1/activities/upload",
            TestData.Upload("share.gpx", TestData.Gpx(name, new DateTime(2026, 8, day, 8, 0, 0, DateTimeKind.Utc))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = (await response.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        return (activity.GetProperty("id").GetInt32(), activity.GetProperty("name").GetString()!, activity.GetProperty("activity_type").GetInt32());
    }

    static async Task SetVisibilityAsync(ApiClient client, int id, string name, int type, int visibility)
    {
        var response = await client.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = name, Description = (string?)null, ActivityType = type, Visibility = visibility }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Followers_see_followers_and_public_activities_but_never_private_ones()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "sharing.owner");
        await api.CreateUserAsync(admin, "sharing.fan");
        await api.CreateUserAsync(admin, "sharing.stranger");
        var owner = await api.LoginAsync("sharing.owner", "Str0ng-pass!");
        var fan = await api.LoginAsync("sharing.fan", "Str0ng-pass!");
        var stranger = await api.LoginAsync("sharing.stranger", "Str0ng-pass!");
        int ownerId = await IdAsync(owner), fanId = await IdAsync(fan);

        var pub = await UploadAsync(owner, "Sharing public", 1);
        var fol = await UploadAsync(owner, "Sharing followers", 2);
        var priv = await UploadAsync(owner, "Sharing private", 3);
        await SetVisibilityAsync(owner, pub.Id, pub.Name, pub.Type, Visibilities.Public);
        await SetVisibilityAsync(owner, fol.Id, fol.Name, fol.Type, Visibilities.Followers);
        // Upload defaults to private, so priv needs no update.

        Assert.Equal(HttpStatusCode.Created, (await fan.Http.PostAsync($"/api/v1/users/{ownerId}/followers", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PostAsync($"/api/v1/follow-requests/{fanId}/accept", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await fan.Http.GetAsync($"/api/v1/activities/{pub.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fan.Http.GetAsync($"/api/v1/activities/{fol.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fan.Http.GetAsync($"/api/v1/activities/{priv.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fan.Http.GetAsync($"/api/v1/activities/{fol.Id}/streams")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fan.Http.GetAsync($"/api/v1/activities/{priv.Id}/streams")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.Http.GetAsync($"/api/v1/activities/{pub.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{fol.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.GetAsync($"/api/v1/activities/{priv.Id}")).StatusCode);

        var shared = await fan.GetJsonAsync<JsonElement>($"/api/v1/activities/{pub.Id}");
        Assert.Equal(ownerId, shared.GetProperty("owner").GetProperty("id").GetInt32());
        Assert.Equal("sharing.owner", shared.GetProperty("owner").GetProperty("name").GetString());

        Assert.Equal(2, (await fan.GetJsonAsync<JsonElement>($"/api/v1/users/{ownerId}/activities")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await stranger.GetJsonAsync<JsonElement>($"/api/v1/users/{ownerId}/activities")).GetProperty("total").GetInt32());
        Assert.Equal(3, (await owner.GetJsonAsync<JsonElement>($"/api/v1/users/{ownerId}/activities")).GetProperty("total").GetInt32());
        Assert.Equal(2, (await fan.GetJsonAsync<JsonElement>("/api/v1/activities/feed")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await stranger.GetJsonAsync<JsonElement>("/api/v1/activities/feed")).GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.DeleteAsync($"/api/v1/users/{ownerId}/followers/{fanId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fan.Http.GetAsync($"/api/v1/activities/{fol.Id}")).StatusCode);
        Assert.Equal(0, (await fan.GetJsonAsync<JsonElement>("/api/v1/activities/feed")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await fan.Http.GetAsync($"/api/v1/activities/{pub.Id}")).StatusCode);
    }
}
