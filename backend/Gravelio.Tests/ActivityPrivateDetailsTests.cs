using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class ActivityPrivateDetailsTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    /// An owner and a stranger, and the owner's public activity (with heart rate).
    async Task<(ApiClient Owner, ApiClient Stranger, int Id)> SetUpAsync(string name, int day)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, $"{name}.owner", Password);
        await api.CreateUserAsync(admin, $"{name}.stranger", Password);
        var owner = await api.LoginAsync($"{name}.owner", Password);
        var stranger = await api.LoginAsync($"{name}.stranger", Password);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload",
            TestData.Upload("private.gpx", TestData.Gpx("Private parts", new DateTime(2026, 9, day, 8, 0, 0, DateTimeKind.Utc), heartRate: true)));
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        var publish = await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Private parts", Description = (string?)null, ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = Visibilities.Public }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return (owner, stranger, id);
    }

    static Task<HttpResponseMessage> SaveAsync(ApiClient client, int id, object body) =>
        client.Http.PutAsJsonAsync($"/api/v1/activities/{id}/private", body, ApiClient.Json);

    static async Task<int[]> StreamTypesAsync(ApiClient client, int id) =>
        (await client.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/streams")).Select(s => s.GetProperty("stream_type").GetInt32()).ToArray();

    [Fact]
    public async Task Notes_and_effort_belong_to_the_owner_and_never_appear_in_what_others_read()
    {
        var (owner, stranger, id) = await SetUpAsync("notes", 1);

        var save = await SaveAsync(owner, id, new { PrivateNotes = "Legs felt heavy", WorkoutFeeling = 40, WorkoutRpe = 70 });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var mine = await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}/private");
        Assert.Equal("Legs felt heavy", mine.GetProperty("private_notes").GetString());
        Assert.Equal(40, mine.GetProperty("workout_feeling").GetInt32());
        Assert.Equal(70, mine.GetProperty("workout_rpe").GetInt32());

        // The stranger can open the public activity, but nothing of the owner-only part reaches them, in any route.
        var shared = await stranger.Http.GetStringAsync($"/api/v1/activities/{id}");
        Assert.DoesNotContain("heavy", shared);
        Assert.DoesNotContain("private_notes", shared);
        Assert.DoesNotContain("workout_rpe", shared);
        Assert.DoesNotContain("hidden", shared);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.Http.GetAsync($"/api/v1/activities/{id}/private")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SaveAsync(stranger, id, new { PrivateNotes = "mine now" })).StatusCode);
        var listed = await stranger.Http.GetStringAsync($"/api/v1/users/{(await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32()}/activities");
        Assert.DoesNotContain("heavy", listed);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(101, 50)]
    [InlineData(50, 9)]
    [InlineData(50, 101)]
    public async Task Feeling_and_exertion_stay_in_range(int feeling, int rpe)
    {
        var (owner, _, id) = await SetUpAsync($"range{Math.Abs(feeling)}x{rpe}", 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(owner, id, new { WorkoutFeeling = feeling, WorkoutRpe = rpe })).StatusCode);
    }

    [Fact]
    public async Task An_unknown_detail_name_is_rejected()
    {
        var (owner, _, id) = await SetUpAsync("badname", 3);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(owner, id, new { HiddenDetails = new[] { "hr", "shoe_size" } })).StatusCode);
    }

    [Fact]
    public async Task What_an_activity_hides_replaces_the_owners_privacy_settings_for_that_activity()
    {
        var (owner, stranger, id) = await SetUpAsync("override", 4);

        // Nothing is hidden by default.
        Assert.NotEqual(JsonValueKind.Null, (await stranger.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("average_hr").ValueKind);
        Assert.Contains(1, await StreamTypesAsync(stranger, id));

        // This activity hides heart rate, the map and the start time.
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(owner, id, new { HiddenDetails = new[] { "hr", "map", "start_time" } })).StatusCode);
        var masked = await stranger.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}");
        Assert.Equal(JsonValueKind.Null, masked.GetProperty("average_hr").ValueKind);
        Assert.Equal(JsonValueKind.Null, masked.GetProperty("start_time").ValueKind);
        var types = await StreamTypesAsync(stranger, id);
        Assert.DoesNotContain(1, types);
        Assert.DoesNotContain(7, types);
        Assert.Contains(4, types); // elevation was not hidden

        // The owner still sees everything.
        Assert.NotEqual(JsonValueKind.Null, (await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("average_hr").ValueKind);
        Assert.Contains(7, await StreamTypesAsync(owner, id));

        // The owner's own choice is readable back, and an empty list means "hide nothing" even if the settings hide things.
        Assert.Equal(new[] { "hr", "map", "start_time" }, (await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}/private")).GetProperty("hidden_details").EnumerateArray().Select(e => e.GetString()!).Order());
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = Visibilities.Public, HideActivityHr = true }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(owner, id, new { HiddenDetails = Array.Empty<string>() })).StatusCode);
        Assert.NotEqual(JsonValueKind.Null, (await stranger.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("average_hr").ValueKind);

        // No choice at all goes back to the settings, which hide heart rate.
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(owner, id, new { })).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await stranger.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("average_hr").ValueKind);
        Assert.DoesNotContain(1, await StreamTypesAsync(stranger, id));
        await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", new { DefaultActivityVisibility = Visibilities.Private }, ApiClient.Json);
    }
}
