using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthWaterTests(ApiFixture api)
{
    [Fact]
    public async Task Water_posts_accumulate_per_day_and_the_list_can_be_edited_and_deleted()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "water.user");
        var user = await api.LoginAsync("water.user", "Str0ng-pass!");

        var first = await user.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2026-05-01", AmountMl = 250m }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstEntry = await first.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        int id = firstEntry.GetProperty("id").GetInt32();
        Assert.Equal(250m, firstEntry.GetProperty("amount_ml").GetDecimal());

        var second = await user.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2026-05-01", AmountMl = 250m }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(500m, (await second.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("amount_ml").GetDecimal());

        var list = await user.GetJsonAsync<JsonElement>("/api/v1/health/water");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(500m, list.GetProperty("items")[0].GetProperty("amount_ml").GetDecimal());

        var update = await user.Http.PutAsJsonAsync($"/api/v1/health/water/{id}", new { Date = "2026-05-01", AmountMl = 750m }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(750m, (await update.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("amount_ml").GetDecimal());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/v1/health/water/{id}")).StatusCode);
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/health/water")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Water_validation_and_ownership_are_enforced()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "water.owner");
        await api.CreateUserAsync(admin, "water.other");
        var owner = await api.LoginAsync("water.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("water.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2026-05-02", AmountMl = -1m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2026-05-02", AmountMl = 20_001m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PostAsJsonAsync("/api/v1/health/water", new { AmountMl = 1m }, ApiClient.Json)).StatusCode);

        var created = await owner.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2026-05-02", AmountMl = 500m }, ApiClient.Json);
        int id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Http.PutAsJsonAsync($"/api/v1/health/water/{id}", new { Date = "2026-05-03", AmountMl = 1m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/health/water/{id}")).StatusCode);
        Assert.Equal(0, (await other.GetJsonAsync<JsonElement>("/api/v1/health/water")).GetProperty("total").GetInt32());
    }
}
