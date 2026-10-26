using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class DailyStatsTests(ApiFixture api)
{
    [Fact]
    public async Task Todays_numbers_come_together_for_the_person_and_nobody_elses()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "daily.me");
        await api.CreateUserAsync(admin, "daily.other");
        var me = await api.LoginAsync("daily.me", "Str0ng-pass!");
        var other = await api.LoginAsync("daily.other", "Str0ng-pass!");

        var empty = await me.GetJsonAsync<JsonElement>("/api/v1/health/stats/daily");
        Assert.Equal(0, empty.GetProperty("poop_count").GetInt32());
        foreach (var field in new[] { "sleep", "weight", "steps", "fasting", "water_ml" }) Assert.Equal(JsonValueKind.Null, empty.GetProperty(field).ValueKind);

        var today = empty.GetProperty("date").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await me.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = today, Steps = 8123 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await me.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = today, AmountMl = 750m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await me.Http.PostAsJsonAsync("/api/v1/health/weight", new { Date = "2020-01-01", Weight = 71.5m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await me.Http.PostAsJsonAsync("/api/v1/health/poop", new { DateTime = DateTime.UtcNow.ToString("O"), BristolType = 4 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await me.Http.PostAsJsonAsync("/api/v1/health/fasting", new { FastStartTime = DateTime.UtcNow.AddHours(-3).ToString("O"), TargetDurationSeconds = 57600 }, ApiClient.Json)).StatusCode);

        var stats = await me.GetJsonAsync<JsonElement>("/api/v1/health/stats/daily");
        Assert.Equal(8123, stats.GetProperty("steps").GetInt32());
        Assert.Equal(750m, stats.GetProperty("water_ml").GetDecimal());
        // The latest weight, whatever day it was logged.
        Assert.Equal(71.5m, stats.GetProperty("weight").GetProperty("weight").GetDecimal());
        Assert.Equal(1, stats.GetProperty("poop_count").GetInt32());
        Assert.Equal("in_progress", stats.GetProperty("fasting").GetProperty("status").GetString());

        var theirs = await other.GetJsonAsync<JsonElement>("/api/v1/health/stats/daily");
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("steps").ValueKind);
        Assert.Equal(0, theirs.GetProperty("poop_count").GetInt32());
    }
}
