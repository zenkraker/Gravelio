using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class GoalsTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    static object Goal(string interval = "weekly", string group = "run", string metric = "distance", int target = 10_000) =>
        new { Interval = interval, ActivityGroup = group, Metric = metric, Target = target };

    /// Uploads a short activity (about 3.6 km) that starts at the given UTC instant, near Lisbon, so its local day is UTC+1 in summer.
    static async Task<double> UploadAsync(ApiClient client, DateTime startUtc, string sport)
    {
        var response = await client.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("goal.gpx", TestData.Gpx("Goal", startUtc, sport: sport)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("distance").GetInt32();
    }

    static async Task<JsonElement> ProgressAsync(ApiClient client, string date, int goalId) =>
        (await client.GetJsonAsync<JsonElement[]>($"/api/v1/goals/progress?date={date}")).Single(g => g.GetProperty("goal_id").GetInt32() == goalId);

    static async Task<int> CreateAsync(ApiClient client, object body)
    {
        var response = await client.Http.PostAsJsonAsync("/api/v1/goals", body, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Goals_are_created_edited_and_deleted_and_belong_to_their_owner()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "goals.owner", Password);
        await api.CreateUserAsync(admin, "goals.other", Password);
        var owner = await api.LoginAsync("goals.owner", Password);
        var other = await api.LoginAsync("goals.other", Password);

        var id = await CreateAsync(owner, Goal());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Http.PostAsJsonAsync("/api/v1/goals", Goal(target: 20_000), ApiClient.Json)).StatusCode);

        var update = await owner.Http.PutAsJsonAsync($"/api/v1/goals/{id}", Goal(metric: "activities", target: 3), ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("activities", (await update.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("metric").GetString());

        // Another user's goal is indistinguishable from a missing one.
        Assert.Empty(await other.GetJsonAsync<JsonElement[]>("/api/v1/goals"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PutAsJsonAsync($"/api/v1/goals/{id}", Goal(), ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/goals/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.DeleteAsync($"/api/v1/goals/{id}")).StatusCode);
        Assert.Empty(await owner.GetJsonAsync<JsonElement[]>("/api/v1/goals"));
    }

    [Theory]
    [InlineData("hourly", "run", "distance", 100)]
    [InlineData("weekly", "dancing", "distance", 100)]
    [InlineData("weekly", "run", "happiness", 100)]
    [InlineData("weekly", "run", "distance", 0)]
    [InlineData("weekly", "run", "distance", 100_000_001)]
    public async Task A_goal_must_name_a_known_period_activity_and_measure_with_a_positive_target(string interval, string group, string metric, int target)
    {
        var admin = await api.LoginAsync();
        var response = await admin.Http.PostAsJsonAsync("/api/v1/goals", Goal(interval, group, metric, target), ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Progress_counts_the_activities_of_the_goals_group_inside_its_period()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "goals.progress", Password);
        var user = await api.LoginAsync("goals.progress", Password);

        // 8 and 9 June 2026 are Monday and Tuesday; the 20th is in another week of the same month.
        var run1 = await UploadAsync(user, new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc), "running");
        var run2 = await UploadAsync(user, new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc), "running");
        await UploadAsync(user, new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc), "running");
        await UploadAsync(user, new DateTime(2026, 6, 9, 17, 0, 0, DateTimeKind.Utc), "cycling");

        var weekDistance = await CreateAsync(user, Goal("weekly", "run", "distance", 14_000));
        var monthRuns = await CreateAsync(user, Goal("monthly", "run", "activities", 4));
        var dayRides = await CreateAsync(user, Goal("daily", "bike", "activities", 1));
        var yearStrength = await CreateAsync(user, Goal("yearly", "strength", "duration", 3600));

        var week = await ProgressAsync(user, "2026-06-10", weekDistance);
        Assert.Equal(run1 + run2, week.GetProperty("total").GetDouble());
        Assert.Equal((int)Math.Round((run1 + run2) * 100 / 14_000), week.GetProperty("percent").GetInt32());
        Assert.Equal("2026-06-08", week.GetProperty("start").GetString());
        Assert.Equal("2026-06-14", week.GetProperty("end").GetString());

        var month = await ProgressAsync(user, "2026-06-10", monthRuns);
        Assert.Equal(3, month.GetProperty("total").GetDouble());
        Assert.Equal(75, month.GetProperty("percent").GetInt32());

        Assert.Equal(100, (await ProgressAsync(user, "2026-06-09", dayRides)).GetProperty("percent").GetInt32());
        Assert.Equal(0, (await ProgressAsync(user, "2026-06-10", dayRides)).GetProperty("percent").GetInt32());
        Assert.Equal(0, (await ProgressAsync(user, "2026-06-10", yearStrength)).GetProperty("total").GetDouble());
    }

    [Fact]
    public async Task Progress_is_capped_at_one_hundred_percent()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "goals.cap", Password);
        var user = await api.LoginAsync("goals.cap", Password);
        await UploadAsync(user, new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc), "running");
        var id = await CreateAsync(user, Goal("daily", "run", "distance", 1000));

        Assert.Equal(100, (await ProgressAsync(user, "2026-05-04", id)).GetProperty("percent").GetInt32());
    }

    [Fact]
    public async Task An_activity_belongs_to_the_local_day_it_was_recorded_on_not_the_UTC_day()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "goals.local", Password);
        var user = await api.LoginAsync("goals.local", Password);
        // 23:30 UTC on Sunday 14 June is 00:30 on Monday 15 June in Lisbon, so it belongs to the next week.
        await UploadAsync(user, new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc), "running");
        var id = await CreateAsync(user, Goal("weekly", "run", "activities", 1));

        Assert.Equal(0, (await ProgressAsync(user, "2026-06-10", id)).GetProperty("total").GetDouble());
        Assert.Equal(1, (await ProgressAsync(user, "2026-06-15", id)).GetProperty("total").GetDouble());
    }
}
