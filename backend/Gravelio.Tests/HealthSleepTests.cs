using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthSleepTests(ApiFixture api)
{
    [Fact]
    public async Task Sleep_is_upserted_per_day_scored_listed_updated_and_deleted()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "sleep.user");
        var user = await api.LoginAsync("sleep.user", "Str0ng-pass!");

        var first = await user.Http.PostAsJsonAsync("/api/v1/health/sleep", new
        {
            Date = "2026-04-01",
            TotalSleepSeconds = 28800,
            DeepSleepSeconds = 5184,
            LightSleepSeconds = 14400,
            RemSleepSeconds = 6480,
            AwakeSleepSeconds = 576,
            AwakeCount = 1,
            AvgSleepStress = 20,
            SleepStages = new[] { new { stage_type = 0, duration_seconds = 600 } },
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        int id = created.GetProperty("id").GetInt32();
        Assert.Equal(98, created.GetProperty("sleep_score_overall").GetInt32());
        Assert.Equal("EXCELLENT", created.GetProperty("sleep_score_duration").GetString());
        Assert.Equal("EXCELLENT", created.GetProperty("sleep_score_quality").GetString());
        Assert.Equal(JsonValueKind.Array, created.GetProperty("sleep_stages").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("source").ValueKind);

        // A second save for the day replaces the record, including a null stages value.
        var second = await user.Http.PostAsJsonAsync("/api/v1/health/sleep", new
        {
            Date = "2026-04-01",
            TotalSleepSeconds = 21600,
            AwakeCount = 1,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var replaced = await second.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(id, replaced.GetProperty("id").GetInt32());
        Assert.Equal(21600, replaced.GetProperty("total_sleep_seconds").GetInt32());
        Assert.Equal("POOR", replaced.GetProperty("sleep_score_quality").GetString());

        var list = await user.GetJsonAsync<JsonElement>("/api/v1/health/sleep");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(21600, list.GetProperty("items")[0].GetProperty("total_sleep_seconds").GetInt32());

        var update = await user.Http.PutAsJsonAsync($"/api/v1/health/sleep/{id}", new
        {
            Date = "2026-04-02",
            TotalSleepSeconds = 28800,
            DeepSleepSeconds = 5184,
            LightSleepSeconds = 14400,
            RemSleepSeconds = 6480,
            AwakeSleepSeconds = 576,
            AwakeCount = 1,
            AvgSleepStress = 20,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal("2026-04-02", updated.GetProperty("date").GetString());
        Assert.Equal(98, updated.GetProperty("sleep_score_overall").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/v1/health/sleep/{id}")).StatusCode);
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/health/sleep")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Sleep_validation_and_ownership_are_enforced()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "sleep.owner");
        await api.CreateUserAsync(admin, "sleep.other");
        var owner = await api.LoginAsync("sleep.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("sleep.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/sleep",
            new { Date = "2026-04-03", TotalSleepSeconds = -1 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/sleep",
            new { Date = "2026-04-03", AvgSpo2 = 50 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/sleep",
            new
            {
                Date = "2026-04-03",
                SleepStartTimeLocal = "2026-04-03T07:00:00Z",
                SleepEndTimeLocal = "2026-04-03T06:00:00Z",
            }, ApiClient.Json)).StatusCode);

        var created = await owner.Http.PostAsJsonAsync("/api/v1/health/sleep",
            new { Date = "2026-04-04", TotalSleepSeconds = 28800 }, ApiClient.Json);
        int id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Http.PutAsJsonAsync($"/api/v1/health/sleep/{id}", new { Date = "2026-04-05", TotalSleepSeconds = 1 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/health/sleep/{id}")).StatusCode);
        Assert.Equal(0, (await other.GetJsonAsync<JsonElement>("/api/v1/health/sleep")).GetProperty("total").GetInt32());
    }
}
