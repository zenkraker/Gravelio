using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthFastingTests(ApiFixture api)
{
    [Fact]
    public async Task Fasting_session_starts_completes_scores_and_deletes()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "fasting.user");
        var user = await api.LoginAsync("fasting.user", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.NotFound, (await user.Http.GetAsync("/api/v1/health/fasting/active")).StatusCode);

        var created = await user.Http.PostAsJsonAsync("/api/v1/health/fasting", new
        {
            FastStartTime = "2026-06-01T20:00:00Z",
            TargetDurationSeconds = 57_600,
            FastingType = "16:8",
            Notes = "first fast",
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fast = await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        int id = fast.GetProperty("id").GetInt32();
        Assert.Equal("in_progress", fast.GetProperty("status").GetString());
        Assert.Equal("manual", fast.GetProperty("source").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PostAsJsonAsync("/api/v1/health/fasting",
            new { FastStartTime = "2026-06-02T20:00:00Z" }, ApiClient.Json)).StatusCode);
        Assert.Equal(id, (await user.GetJsonAsync<JsonElement>("/api/v1/health/fasting/active")).GetProperty("id").GetInt32());
        Assert.Equal(1, (await user.GetJsonAsync<JsonElement>("/api/v1/health/fasting")).GetProperty("total").GetInt32());

        var completed = await user.Http.PostAsJsonAsync($"/api/v1/health/fasting/{id}/complete",
            new { FastEndTime = "2026-06-02T12:00:00Z", Status = "completed" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var done = await completed.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal("completed", done.GetProperty("status").GetString());
        Assert.Equal(57_600, done.GetProperty("actual_duration_seconds").GetInt32());

        var stats = await user.GetJsonAsync<JsonElement>("/api/v1/health/fasting/stats");
        Assert.Equal(1, stats.GetProperty("total_fasts").GetInt32());
        Assert.Equal(57_600, stats.GetProperty("total_fasting_seconds").GetInt32());
        Assert.Equal(100.0, stats.GetProperty("completion_rate").GetDouble());
        Assert.Equal(1, stats.GetProperty("longest_streak").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/v1/health/fasting/{id}")).StatusCode);
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/health/fasting")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Fasting_validation_ownership_and_streaks_are_enforced()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "fasting.owner");
        await api.CreateUserAsync(admin, "fasting.other");
        var owner = await api.LoginAsync("fasting.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("fasting.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/fasting",
            new { FastStartTime = "2026-06-01T20:00:00Z", TargetDurationSeconds = 259_201 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/fasting",
            new { FastStartTime = "2026-06-01T20:00:00Z", FastingType = "nope" }, ApiClient.Json)).StatusCode);

        // Three consecutive completed fasts give a longest streak of three.
        for (int day = 1; day <= 3; day++)
        {
            var created = await owner.Http.PostAsJsonAsync("/api/v1/health/fasting",
                new { FastStartTime = $"2026-06-0{day}T20:00:00Z", FastingType = "16:8" }, ApiClient.Json);
            int id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
            Assert.Equal(HttpStatusCode.OK, (await owner.Http.PostAsJsonAsync($"/api/v1/health/fasting/{id}/complete",
                new { FastEndTime = $"2026-06-0{day + 1}T12:00:00Z", Status = "completed" }, ApiClient.Json)).StatusCode);
        }
        var stats = await owner.GetJsonAsync<JsonElement>("/api/v1/health/fasting/stats");
        Assert.Equal(3, stats.GetProperty("longest_streak").GetInt32());
        Assert.Equal(0, stats.GetProperty("current_streak").GetInt32());

        var active = await owner.Http.PostAsJsonAsync("/api/v1/health/fasting",
            new { FastStartTime = "2026-06-10T20:00:00Z" }, ApiClient.Json);
        int activeId = (await active.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PostAsJsonAsync($"/api/v1/health/fasting/{activeId}/complete",
            new { FastEndTime = "2026-06-11T12:00:00Z" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/health/fasting/{activeId}")).StatusCode);

        // Ending before starting is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync($"/api/v1/health/fasting/{activeId}/complete",
            new { FastEndTime = "2026-06-10T19:00:00Z" }, ApiClient.Json)).StatusCode);
    }
}
