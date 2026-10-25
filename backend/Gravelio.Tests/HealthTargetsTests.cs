using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthTargetsTests(ApiFixture api)
{
    [Fact]
    public async Task Targets_default_to_nulls_and_are_upserted()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "targets.user");
        var user = await api.LoginAsync("targets.user", "Str0ng-pass!");

        var empty = await user.GetJsonAsync<JsonElement>("/api/v1/health/targets");
        Assert.Equal(0, empty.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("steps").ValueKind);

        var saved = await user.Http.PutAsJsonAsync("/api/v1/health/targets", new
        {
            Weight = 70.5m,
            Steps = 10_000,
            Sleep = 28_800,
            Fasting = 57_600,
            WaterMl = 2_000m,
            PoopCount = 1,
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var target = await saved.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.True(target.GetProperty("id").GetInt32() > 0);
        Assert.Equal(10_000, target.GetProperty("steps").GetInt32());
        Assert.Equal(2_000m, target.GetProperty("water_ml").GetDecimal());

        var read = await user.GetJsonAsync<JsonElement>("/api/v1/health/targets");
        Assert.Equal(28_800, read.GetProperty("sleep").GetInt32());
        Assert.Equal(1, read.GetProperty("poop_count").GetInt32());
    }

    [Fact]
    public async Task Targets_validation_and_per_user_isolation_are_enforced()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "targets.owner");
        await api.CreateUserAsync(admin, "targets.other");
        var owner = await api.LoginAsync("targets.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("targets.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PutAsJsonAsync("/api/v1/health/targets", new { Weight = 0m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PutAsJsonAsync("/api/v1/health/targets", new { Sleep = 90_000 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.Http.PutAsJsonAsync("/api/v1/health/targets", new { PoopCount = 21 }, ApiClient.Json)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await owner.Http.PutAsJsonAsync("/api/v1/health/targets", new { Steps = 8_000 }, ApiClient.Json)).StatusCode);
        var otherTargets = await other.GetJsonAsync<JsonElement>("/api/v1/health/targets");
        Assert.Equal(0, otherTargets.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, otherTargets.GetProperty("steps").ValueKind);
    }
}
