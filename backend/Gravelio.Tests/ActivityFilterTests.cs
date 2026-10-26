using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class ActivityFilterTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    static async Task<int> UploadAsync(ApiClient client, string name, DateTime startUtc, string sport = "running", int points = 60)
    {
        var response = await client.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("filter.gpx", TestData.Gpx(name, startUtc, points, sport)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("id").GetInt32();
    }

    static async Task<string[]> NamesAsync(ApiClient client, string query)
    {
        var page = await client.GetJsonAsync<JsonElement>($"/api/v1/activities?{query}");
        return page.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("name").GetString()!).ToArray();
    }

    async Task<ApiClient> UserAsync(string username)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        return await api.LoginAsync(username, Password);
    }

    [Fact]
    public async Task The_list_filters_by_type_name_and_gear_and_counts_only_what_matches()
    {
        var user = await UserAsync("filter.basic");
        await UploadAsync(user, "Morning jog", new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc));
        var ride = await UploadAsync(user, "Gravel 100% fun", new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), "cycling");
        await UploadAsync(user, "Evening ride", new DateTime(2026, 3, 3, 8, 0, 0, DateTimeKind.Utc), "cycling");

        var gear = await user.Http.PostAsJsonAsync("/api/v1/gears", new { Nickname = "Filter bike", GearType = 1 }, ApiClient.Json);
        var gearId = (await gear.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await user.Http.PutAsJsonAsync($"/api/v1/activities/{ride}/gear", new { GearId = gearId }, ApiClient.Json)).StatusCode);

        Assert.Equal(["Evening ride", "Gravel 100% fun"], await NamesAsync(user, "type=4"));
        Assert.Equal(["Morning jog"], await NamesAsync(user, "type=1"));
        Assert.Equal(["Evening ride"], await NamesAsync(user, "q=EVENING"));
        // A search for a percent sign matches the text, not everything.
        Assert.Equal(["Gravel 100% fun"], await NamesAsync(user, "q=%25"));
        Assert.Equal(["Gravel 100% fun"], await NamesAsync(user, $"gear_id={gearId}"));
        Assert.Equal(3, (await user.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await user.GetJsonAsync<JsonElement>("/api/v1/activities?type=1")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_date_range_uses_the_local_day_each_activity_was_recorded_on()
    {
        var user = await UserAsync("filter.dates");
        await UploadAsync(user, "Before", new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));
        // 23:30 UTC on 14 June is 00:30 on 15 June in Lisbon, where the activity was recorded.
        await UploadAsync(user, "After midnight", new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc));
        await UploadAsync(user, "Later", new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(["After midnight", "Later"], (await NamesAsync(user, "from=2026-06-15")).Reverse());
        Assert.DoesNotContain("After midnight", await NamesAsync(user, "to=2026-06-14"));
        Assert.Equal(["After midnight"], await NamesAsync(user, "from=2026-06-15&to=2026-06-15"));
        Assert.Equal(["Before"], await NamesAsync(user, "to=2026-06-14"));
    }

    [Fact]
    public async Task The_list_sorts_by_a_chosen_field_in_either_order()
    {
        var user = await UserAsync("filter.sort");
        await UploadAsync(user, "Medium", new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), points: 60);
        await UploadAsync(user, "Short", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc), points: 30);
        await UploadAsync(user, "Long", new DateTime(2026, 4, 3, 8, 0, 0, DateTimeKind.Utc), points: 90);

        Assert.Equal(["Short", "Medium", "Long"], await NamesAsync(user, "sort=distance&order=asc"));
        Assert.Equal(["Long", "Medium", "Short"], await NamesAsync(user, "sort=distance&order=desc"));
        Assert.Equal(["Long", "Medium", "Short"], await NamesAsync(user, "sort=name&order=asc"));
        Assert.Equal(["Long", "Short", "Medium"], await NamesAsync(user, "")); // newest first by default
    }

    [Theory]
    [InlineData("type=9999")]
    [InlineData("sort=happiness")]
    [InlineData("order=sideways")]
    [InlineData("from=2026-06-15&to=2026-06-01")]
    public async Task Unknown_values_are_rejected(string query)
    {
        var admin = await api.LoginAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Http.GetAsync($"/api/v1/activities?{query}")).StatusCode);
    }
}
