using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class DuplicateActivityTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";
    static readonly DateTime Start = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);

    async Task<(ApiClient Owner, ApiClient Stranger)> UsersAsync(string name)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, $"{name}.owner", Password);
        await api.CreateUserAsync(admin, $"{name}.stranger", Password);
        return (await api.LoginAsync($"{name}.owner", Password), await api.LoginAsync($"{name}.stranger", Password));
    }

    static async Task<HttpResponseMessage> UploadAsync(ApiClient client, string name, DateTime start) =>
        await client.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("dup.gpx", TestData.Gpx(name, start)));

    static async Task<JsonElement> OnlyAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
    }

    [Fact]
    public async Task The_same_file_again_is_refused_but_a_different_file_at_the_same_start_is_kept_hidden_and_reported()
    {
        var (owner, _) = await UsersAsync("dup.basic");
        var first = await OnlyAsync(await UploadAsync(owner, "Watch recording", Start));
        Assert.False(first.GetProperty("is_hidden").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(owner, "Watch recording", Start)).StatusCode);

        var second = await OnlyAsync(await UploadAsync(owner, "Phone recording", Start));
        Assert.True(second.GetProperty("is_hidden").GetBoolean());
        var secondId = second.GetProperty("id").GetInt32();

        // The owner is told, with a way to open the activity.
        var notes = (await owner.GetJsonAsync<JsonElement>("/api/v1/notifications")).GetProperty("items").EnumerateArray().ToList();
        var note = Assert.Single(notes, n => n.GetProperty("type").GetInt32() == 2);
        Assert.Equal(secondId, note.GetProperty("activity_id").GetInt32());
        Assert.Equal("Phone recording", note.GetProperty("activity_name").GetString());

        // Uploading that second file again is refused too.
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(owner, "Phone recording", Start)).StatusCode);
        Assert.Equal(2, (await owner.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_hidden_activity_is_left_out_of_what_others_see_and_of_the_totals_until_the_owner_unhides_it()
    {
        var (owner, stranger) = await UsersAsync("dup.hidden");
        var first = await OnlyAsync(await UploadAsync(owner, "Kept", Start));
        var duplicate = await OnlyAsync(await UploadAsync(owner, "Duplicate", Start));
        var duplicateId = duplicate.GetProperty("id").GetInt32();
        var ownerId = (await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

        // Both are public, so only hiding keeps the duplicate out.
        foreach (var activity in new[] { first, duplicate })
        {
            var id = activity.GetProperty("id").GetInt32();
            await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
                new { Name = activity.GetProperty("name").GetString(), Description = (string?)null, ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = Visibilities.Public }, ApiClient.Json);
        }
        var gear = await owner.Http.PostAsJsonAsync("/api/v1/gears", new { Nickname = "Dup shoes", GearType = 2 }, ApiClient.Json);
        var gearId = (await gear.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        await owner.Http.PutAsJsonAsync($"/api/v1/activities/{duplicateId}/gear", new { GearId = gearId }, ApiClient.Json);
        var goal = await owner.Http.PostAsJsonAsync("/api/v1/goals", new { Interval = "yearly", ActivityGroup = "run", Metric = "activities", Target = 10 }, ApiClient.Json);
        var goalId = (await goal.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{duplicateId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{duplicateId}/streams")).StatusCode);
        Assert.Equal(1, (await stranger.GetJsonAsync<JsonElement>($"/api/v1/users/{ownerId}/activities")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await owner.GetJsonAsync<JsonElement>("/api/v1/activities/stats?period=lifetime")).GetProperty("activity_count").GetInt32());
        Assert.Equal(1, (await owner.GetJsonAsync<JsonElement[]>("/api/v1/goals/progress?date=2026-01-10")).Single(g => g.GetProperty("goal_id").GetInt32() == goalId).GetProperty("total").GetDouble());
        Assert.Equal(0, (await owner.GetJsonAsync<JsonElement>($"/api/v1/gears/{gearId}")).GetProperty("activity_count").GetInt32());
        // The owner still sees it, marked hidden.
        Assert.True((await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{duplicateId}")).GetProperty("is_hidden").GetBoolean());

        // Not a duplicate after all: unhide it.
        var unhide = await owner.Http.PutAsJsonAsync($"/api/v1/activities/{duplicateId}/private", new { IsHidden = false }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, unhide.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.Http.GetAsync($"/api/v1/activities/{duplicateId}")).StatusCode);
        Assert.Equal(2, (await owner.GetJsonAsync<JsonElement>("/api/v1/activities/stats?period=lifetime")).GetProperty("activity_count").GetInt32());
        Assert.Equal(1, (await owner.GetJsonAsync<JsonElement>($"/api/v1/gears/{gearId}")).GetProperty("activity_count").GetInt32());
    }

    [Fact]
    public async Task An_owner_can_hide_any_activity_and_it_leaves_every_shared_view()
    {
        var (owner, stranger) = await UsersAsync("dup.manual");
        var activity = await OnlyAsync(await UploadAsync(owner, "Embarrassing", Start.AddDays(1)));
        var id = activity.GetProperty("id").GetInt32();
        await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Embarrassing", Description = (string?)null, ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = Visibilities.Public }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, (await stranger.Http.GetAsync($"/api/v1/activities/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}/private", new { IsHidden = true }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{id}")).StatusCode);
        Assert.True((await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}/private")).GetProperty("is_hidden").GetBoolean());
    }
}
