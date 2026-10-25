using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class HealthWeightTests(ApiFixture api)
{
    const string Weight = "/api/v1/health/weight";

    static Task<HttpResponseMessage> PostAsync(ApiClient client, string date, decimal kg) =>
        client.Http.PostAsJsonAsync(Weight, new { Date = date, Weight = kg, BodyFat = 18.5m }, ApiClient.Json);

    static async Task<int> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

    [Fact]
    public async Task A_second_post_for_the_same_day_replaces_that_days_reading()
    {
        var admin = await api.LoginAsync();
        var first = await PostAsync(admin, "2026-03-01", 80.5m);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await PostAsync(admin, "2026-03-01", 79.9m);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await IdOf(first), await IdOf(second));

        var page = await admin.GetJsonAsync<JsonElement>(Weight);
        var march = page.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("date").GetString() == "2026-03-01").Single();
        Assert.Equal(79.9m, march.GetProperty("weight").GetDecimal());
        Assert.Equal(18.5m, march.GetProperty("body_fat").GetDecimal());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500.5)]
    public async Task Weight_outside_the_allowed_range_is_rejected(double kg)
    {
        var admin = await api.LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(admin, "2026-03-02", (decimal)kg)).StatusCode);
    }

    [Fact]
    public async Task Moving_a_reading_onto_a_taken_day_is_a_conflict()
    {
        var admin = await api.LoginAsync();
        await PostAsync(admin, "2026-03-03", 80m);
        int id = await IdOf(await PostAsync(admin, "2026-03-04", 81m));

        var move = await admin.Http.PutAsJsonAsync($"{Weight}/{id}", new { Date = "2026-03-03", Weight = 81m }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
    }

    [Fact]
    public async Task Readings_are_private_to_their_owner()
    {
        var admin = await api.LoginAsync();
        int id = await IdOf(await PostAsync(admin, "2026-03-05", 82m));
        await api.CreateUserAsync(admin, "wendy");
        var wendy = await api.LoginAsync("wendy", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.NotFound, (await wendy.Http.PutAsJsonAsync($"{Weight}/{id}", new { Date = "2026-03-05", Weight = 1m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await wendy.Http.DeleteAsync($"{Weight}/{id}")).StatusCode);
        Assert.Empty((await wendy.GetJsonAsync<JsonElement>(Weight)).GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"{Weight}/{id}")).StatusCode);
    }
}
