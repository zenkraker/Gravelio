using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthStepsTests(ApiFixture api)
{
    [Fact]
    public async Task Steps_are_upserted_per_day_listed_and_deleted()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "steps.user");
        var user = await api.LoginAsync("steps.user", "Str0ng-pass!");

        var first = await user.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = "2026-03-01", Steps = 8000 }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstEntry = await first.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        int id = firstEntry.GetProperty("id").GetInt32();
        Assert.Equal(JsonValueKind.Null, firstEntry.GetProperty("source").ValueKind);

        var second = await user.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = "2026-03-01", Steps = 9000 }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondEntry = await second.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(id, secondEntry.GetProperty("id").GetInt32());
        Assert.Equal(9000, secondEntry.GetProperty("steps").GetInt32());

        var list = await user.GetJsonAsync<JsonElement>("/api/v1/health/steps");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(9000, list.GetProperty("items")[0].GetProperty("steps").GetInt32());

        var update = await user.Http.PutAsJsonAsync($"/api/v1/health/steps/{id}",
            new { Date = "2026-03-02", Steps = 10_000 }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal("2026-03-02", updated.GetProperty("date").GetString());
        Assert.Equal(10_000, updated.GetProperty("steps").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/v1/health/steps/{id}")).StatusCode);
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/health/steps")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Steps_validation_is_owned_and_other_users_cannot_touch_a_row()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "steps.owner");
        await api.CreateUserAsync(admin, "steps.other");
        var owner = await api.LoginAsync("steps.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("steps.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = "2026-03-03", Steps = -1 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PostAsJsonAsync("/api/v1/health/steps", new { Steps = 1 }, ApiClient.Json)).StatusCode);

        var created = await owner.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = "2026-03-03", Steps = 5000 }, ApiClient.Json);
        int id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Http.PutAsJsonAsync($"/api/v1/health/steps/{id}", new { Date = "2026-03-04", Steps = 1 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/health/steps/{id}")).StatusCode);
        Assert.Equal(0, (await other.GetJsonAsync<JsonElement>("/api/v1/health/steps")).GetProperty("total").GetInt32());
    }
}
