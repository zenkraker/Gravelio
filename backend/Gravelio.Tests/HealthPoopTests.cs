using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthPoopTests(ApiFixture api)
{
    [Fact]
    public async Task Poop_records_can_be_created_edited_and_deleted()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "poop.user");
        var user = await api.LoginAsync("poop.user", "Str0ng-pass!");

        var first = await user.Http.PostAsJsonAsync("/api/v1/health/poop", new
        {
            DateTime = "2026-06-01T08:00:00Z",
            BristolType = 4,
            Color = "brown",
            Notes = "normal",
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstEntry = await first.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        int id = firstEntry.GetProperty("id").GetInt32();
        Assert.Equal(4, firstEntry.GetProperty("bristol_type").GetInt32());
        Assert.Equal("manual", firstEntry.GetProperty("source").GetString());

        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync("/api/v1/health/poop",
            new { DateTime = "2026-06-01T18:00:00Z", BristolType = 3, Color = "dark_brown" }, ApiClient.Json)).StatusCode);
        Assert.Equal(2, (await user.GetJsonAsync<JsonElement>("/api/v1/health/poop")).GetProperty("total").GetInt32());

        var update = await user.Http.PutAsJsonAsync($"/api/v1/health/poop/{id}", new
        {
            DateTime = "2026-06-02T08:00:00Z",
            BristolType = 5,
            Color = "green",
            Notes = "changed",
        }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(5, updated.GetProperty("bristol_type").GetInt32());
        Assert.Equal("green", updated.GetProperty("color").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.DeleteAsync($"/api/v1/health/poop/{id}")).StatusCode);
        Assert.Equal(1, (await user.GetJsonAsync<JsonElement>("/api/v1/health/poop")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Poop_validation_and_ownership_are_enforced()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "poop.owner");
        await api.CreateUserAsync(admin, "poop.other");
        var owner = await api.LoginAsync("poop.owner", "Str0ng-pass!");
        var other = await api.LoginAsync("poop.other", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/poop",
            new { DateTime = "2026-06-01T08:00:00Z", BristolType = 8 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/poop",
            new { DateTime = "2026-06-01T08:00:00Z", Color = "purple" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/health/poop",
            new { BristolType = 4 }, ApiClient.Json)).StatusCode);

        var created = await owner.Http.PostAsJsonAsync("/api/v1/health/poop",
            new { DateTime = "2026-06-01T08:00:00Z", BristolType = 4 }, ApiClient.Json);
        int id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync($"/api/v1/health/poop/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PutAsJsonAsync($"/api/v1/health/poop/{id}",
            new { DateTime = "2026-06-02T08:00:00Z", BristolType = 3 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.DeleteAsync($"/api/v1/health/poop/{id}")).StatusCode);
        Assert.Equal(0, (await other.GetJsonAsync<JsonElement>("/api/v1/health/poop")).GetProperty("total").GetInt32());
    }
}
