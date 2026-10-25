using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Gravelio.Tests.TestData;

namespace Gravelio.Tests;

[Collection("api")]
public class GearTests(ApiFixture api)
{
    const string Gears = "/api/v1/gears";

    static object Bike(string nickname = "Gravel bike") => new { Nickname = nickname, Brand = "Canyon", Model = "Grizl", GearType = 1, InitialDistance = 1000.0, PurchaseValue = 1999.99m };

    static async Task<int> CreateAsync(ApiClient client, object body)
    {
        var response = await client.Http.PostAsJsonAsync(Gears, body, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
    }

    /// Uploads a 3,618 m ride starting on the given day and returns its id.
    static async Task<int> UploadRideAsync(ApiClient client, int day)
    {
        var response = await client.Http.PostAsync("/api/v1/activities/upload", Upload("ride.gpx", Gpx("Ride", new DateTime(2026, 6, day, 8, 0, 0, DateTimeKind.Utc), sport: "cycling")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("id").GetInt32();
    }

    static Task<HttpResponseMessage> PostComponentAsync(ApiClient client, int gearId, string type, object? extra = null) =>
        client.Http.PostAsJsonAsync($"{Gears}/{gearId}/components", new { Type = type, PurchaseDate = "2026-01-15", ExpectedDistance = 5000000.0 }.Merge(extra), ApiClient.Json);

    [Fact]
    public async Task Gear_usage_is_its_initial_distance_plus_the_activities_on_it()
    {
        var admin = await api.LoginAsync();
        int gearId = await CreateAsync(admin, Bike("Usage bike"));
        int activityId = await UploadRideAsync(admin, 1);

        var assign = await admin.Http.PutAsJsonAsync($"/api/v1/activities/{activityId}/gear", new { GearId = gearId }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var gear = await admin.GetJsonAsync<JsonElement>($"{Gears}/{gearId}");
        Assert.Equal(1000 + 3618, gear.GetProperty("total_distance").GetDouble());
        Assert.Equal(1, gear.GetProperty("activity_count").GetInt32());
        Assert.Equal(1999.99m, gear.GetProperty("purchase_value").GetDecimal());
    }

    [Fact]
    public async Task Gear_is_private_to_its_owner()
    {
        var admin = await api.LoginAsync();
        int gearId = await CreateAsync(admin, Bike("Admin only"));
        await api.CreateUserAsync(admin, "gwen");
        var gwen = await api.LoginAsync("gwen", "Str0ng-pass!");

        Assert.Equal(HttpStatusCode.NotFound, (await gwen.Http.GetAsync($"{Gears}/{gearId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await gwen.Http.PutAsJsonAsync($"{Gears}/{gearId}", Bike("Stolen"), ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await gwen.Http.DeleteAsync($"{Gears}/{gearId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await gwen.Http.GetAsync($"{Gears}/{gearId}/components")).StatusCode);
        Assert.Equal(0, (await gwen.GetJsonAsync<JsonElement>(Gears)).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await PostComponentAsync(gwen, gearId, "chain")).StatusCode);
    }

    [Fact]
    public async Task Components_must_fit_the_gear_type()
    {
        var admin = await api.LoginAsync();
        int bike = await CreateAsync(admin, Bike("Component bike"));
        int shoes = await CreateAsync(admin, new { Nickname = "Component shoes", GearType = 2 });
        int wetsuit = await CreateAsync(admin, new { Nickname = "Component wetsuit", GearType = 3 });

        Assert.Equal(HttpStatusCode.Created, (await PostComponentAsync(admin, bike, "chain")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostComponentAsync(admin, bike, "sail")).StatusCode); // a sail is not a bike part
        Assert.Equal(HttpStatusCode.Created, (await PostComponentAsync(admin, shoes, "laces")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostComponentAsync(admin, shoes, "chain")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostComponentAsync(admin, wetsuit, "laces")).StatusCode); // wetsuits carry none
    }

    [Fact]
    public async Task A_retired_component_is_inactive_and_cannot_be_retired_before_it_was_bought()
    {
        var admin = await api.LoginAsync();
        int bike = await CreateAsync(admin, Bike("Retire bike"));

        var tooEarly = await PostComponentAsync(admin, bike, "chain", new { RetiredDate = "2025-12-31" });
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);

        var retired = await PostComponentAsync(admin, bike, "chain", new { RetiredDate = "2026-03-01", Active = true });
        Assert.Equal(HttpStatusCode.Created, retired.StatusCode);
        Assert.False((await retired.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Component_wear_counts_only_rides_between_buying_and_retiring_it()
    {
        var admin = await api.LoginAsync();
        int bike = await CreateAsync(admin, Bike("Wear bike"));
        int rideId = await UploadRideAsync(admin, 6); // 3,618 m on 2026-06-06
        await admin.Http.PutAsJsonAsync($"/api/v1/activities/{rideId}/gear", new { GearId = bike }, ApiClient.Json);

        async Task<double> UsedAsync(string type, string purchased, string? retired)
        {
            var response = await PostComponentAsync(admin, bike, type, new { PurchaseDate = purchased, RetiredDate = retired });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("used_distance").GetDouble();
        }
        Assert.Equal(3618, await UsedAsync("chain", "2026-05-01", null)); // bought before the ride, still fitted
        Assert.Equal(0, await UsedAsync("saddle", "2026-06-07", null)); // bought after the ride
        Assert.Equal(0, await UsedAsync("fork", "2026-05-01", "2026-06-05")); // retired before the ride
        Assert.Equal(3618, await UsedAsync("stem", "2026-06-06", "2026-06-06")); // bought and retired on the ride's own day

        var listed = await admin.GetJsonAsync<JsonElement[]>($"{Gears}/{bike}/components");
        Assert.Equal(4, listed.Length);
        Assert.Equal(3618, listed.Single(c => c.GetProperty("type").GetString() == "chain").GetProperty("used_distance").GetDouble());
    }

    [Fact]
    public async Task The_server_lists_which_component_types_each_gear_type_allows()
    {
        var admin = await api.LoginAsync();
        var types = await admin.GetJsonAsync<Dictionary<string, string[]>>($"{Gears}/component-types");

        Assert.Contains("chain", types["1"]);
        Assert.DoesNotContain("sail", types["1"]);
        Assert.Contains("laces", types["2"]);
        Assert.Empty(types["3"]); // wetsuits carry no components
        Assert.Equal(8, types.Count);
    }

    [Fact]
    public async Task Another_users_gear_cannot_be_put_on_my_activity()
    {
        var admin = await api.LoginAsync();
        int activityId = await UploadRideAsync(admin, 2);
        await api.CreateUserAsync(admin, "hugo");
        var hugo = await api.LoginAsync("hugo", "Str0ng-pass!");
        int hugosGear = await CreateAsync(hugo, Bike("Hugo's bike"));

        var response = await admin.Http.PutAsJsonAsync($"/api/v1/activities/{activityId}/gear", new { GearId = hugosGear }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await admin.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}")).GetProperty("gear_id").ValueKind);
    }

    [Fact]
    public async Task Deleting_gear_unlinks_its_activities_and_removes_its_components()
    {
        var admin = await api.LoginAsync();
        int gearId = await CreateAsync(admin, Bike("Doomed bike"));
        Assert.Equal(HttpStatusCode.Created, (await PostComponentAsync(admin, gearId, "chain")).StatusCode);
        int activityId = await UploadRideAsync(admin, 3);
        await admin.Http.PutAsJsonAsync($"/api/v1/activities/{activityId}/gear", new { GearId = gearId }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"{Gears}/{gearId}")).StatusCode);

        var activity = await admin.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}"); // still there
        Assert.Equal(JsonValueKind.Null, activity.GetProperty("gear_id").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Http.GetAsync($"{Gears}/{gearId}/components")).StatusCode);
    }

    [Fact]
    public async Task Default_gear_is_applied_to_new_uploads_of_that_activity_type()
    {
        var admin = await api.LoginAsync();
        int gearId = await CreateAsync(admin, Bike("Default bike"));
        const int rideSlot = 4;
        Assert.Equal(HttpStatusCode.OK, (await admin.Http.PutAsJsonAsync($"{Gears}/defaults/{rideSlot}", new { GearId = gearId }, ApiClient.Json)).StatusCode);

        int activityId = await UploadRideAsync(admin, 4);
        Assert.Equal(gearId, (await admin.GetJsonAsync<JsonElement>($"/api/v1/activities/{activityId}")).GetProperty("gear_id").GetInt32());

        // Retired gear is skipped, and clearing the default stops it being applied.
        await admin.Http.PutAsJsonAsync($"{Gears}/{gearId}", new { Nickname = "Default bike", GearType = 1, Active = false }, ApiClient.Json);
        int second = await UploadRideAsync(admin, 5);
        Assert.Equal(JsonValueKind.Null, (await admin.GetJsonAsync<JsonElement>($"/api/v1/activities/{second}")).GetProperty("gear_id").ValueKind);
    }

    [Fact]
    public async Task Default_gear_must_exist_belong_to_me_be_active_and_fit_the_activity_type()
    {
        var admin = await api.LoginAsync();
        int bike = await CreateAsync(admin, Bike("Slot bike"));
        int shoes = await CreateAsync(admin, new { Nickname = "Slot shoes", GearType = 2 });
        int retired = await CreateAsync(admin, new { Nickname = "Slot retired bike", GearType = 1, Active = false });
        await api.CreateUserAsync(admin, "iris");
        var iris = await api.LoginAsync("iris", "Str0ng-pass!");
        int irisBike = await CreateAsync(iris, Bike("Iris bike"));

        Task<HttpResponseMessage> Put(int slot, int? gearId) => admin.Http.PutAsJsonAsync($"{Gears}/defaults/{slot}", new { GearId = gearId }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, (await Put(4, bike)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(4, shoes)).StatusCode); // shoes don't fit a ride
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(4, retired)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(4, irisBike)).StatusCode); // someone else's gear
        Assert.Equal(HttpStatusCode.NotFound, (await Put(99, bike)).StatusCode); // not a slot
        Assert.Equal(HttpStatusCode.OK, (await Put(4, null)).StatusCode); // clear

        var slots = await admin.GetJsonAsync<JsonElement[]>($"{Gears}/defaults");
        Assert.Equal(15, slots.Length);
        Assert.Equal(JsonValueKind.Null, slots.Single(s => s.GetProperty("activity_type").GetInt32() == 4).GetProperty("gear_id").ValueKind);
    }

    [Fact]
    public async Task Gear_type_cannot_change_while_components_or_defaults_depend_on_it()
    {
        var admin = await api.LoginAsync();
        int withComponent = await CreateAsync(admin, Bike("Typed bike"));
        await PostComponentAsync(admin, withComponent, "chain");
        int withDefault = await CreateAsync(admin, Bike("Defaulted bike"));
        await admin.Http.PutAsJsonAsync($"{Gears}/defaults/6", new { GearId = withDefault }, ApiClient.Json);

        Task<HttpResponseMessage> AsShoes(int id) => admin.Http.PutAsJsonAsync($"{Gears}/{id}", new { Nickname = "Now shoes", GearType = 2 }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsShoes(withComponent)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AsShoes(withDefault)).StatusCode);
    }

    [Fact]
    public async Task The_list_pages_filters_and_searches_without_treating_wildcards_as_wildcards()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "jon");
        var jon = await api.LoginAsync("jon", "Str0ng-pass!");
        await CreateAsync(jon, Bike("Road racer"));
        await CreateAsync(jon, Bike("Gravel grinder"));
        await CreateAsync(jon, new { Nickname = "100% rubber", GearType = 2 });
        await CreateAsync(jon, new { Nickname = "Old shoes", GearType = 2, Active = false });

        async Task<JsonElement> List(string query) => await jon.GetJsonAsync<JsonElement>($"{Gears}?{query}");
        Assert.Equal(4, (await List("")).GetProperty("total").GetInt32());
        Assert.Equal(2, (await List("page_size=2")).GetProperty("items").GetArrayLength());
        Assert.Equal("Gravel grinder", (await List("q=GRAVEL")).GetProperty("items")[0].GetProperty("nickname").GetString()); // case-insensitive
        Assert.Equal(2, (await List("gear_type=1")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await List("active=false")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await List("q=100%25")).GetProperty("total").GetInt32()); // "100%" is not "100 followed by anything"
        Assert.Equal(1, (await List("q=%25")).GetProperty("total").GetInt32()); // "%" matches only the nickname holding a literal %, not all four
        Assert.Equal(HttpStatusCode.BadRequest, (await jon.Http.GetAsync($"{Gears}?gear_type=99")).StatusCode);
    }

    [Fact]
    public async Task Currency_is_a_profile_setting_with_three_choices()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "kai");
        var kai = await api.LoginAsync("kai", "Str0ng-pass!");
        object Profile(string currency) => new
        {
            Name = "Kai",
            Email = "kai@example.com",
            PreferredLanguage = "en",
            Gender = "unspecified",
            Units = "metric",
            FirstDayOfWeek = "monday",
            Timezone = "UTC",
            Currency = currency,
        };

        Assert.Equal(HttpStatusCode.OK, (await kai.Http.PutAsJsonAsync("/api/v1/users/me", Profile("pound"), ApiClient.Json)).StatusCode);
        Assert.Equal("pound", (await kai.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("currency").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await kai.Http.PutAsJsonAsync("/api/v1/users/me", Profile("bitcoin"), ApiClient.Json)).StatusCode);
    }

    [Fact]
    public async Task Saving_my_profile_keeps_me_signed_in_even_when_the_email_changes()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "lena");
        var phone = await api.LoginAsync("lena", "Str0ng-pass!");
        var laptop = await api.LoginAsync("lena", "Str0ng-pass!");
        object Profile(string email) => new
        {
            Name = "Lena",
            Email = email,
            PreferredLanguage = "en",
            Gender = "unspecified",
            Units = "metric",
            FirstDayOfWeek = "monday",
            Timezone = "UTC",
        };

        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.PutAsJsonAsync("/api/v1/users/me", Profile("lena@example.com"), ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.Http.GetAsync("/api/v1/users/me")).StatusCode); // nothing changed, nobody signed out

        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.PutAsJsonAsync("/api/v1/users/me", Profile("lena.new@example.com"), ApiClient.Json)).StatusCode);
        Assert.Equal("lena.new@example.com", (await laptop.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("email").GetString()); // this session survives
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.Http.GetAsync("/api/v1/users/me")).StatusCode); // an email change signs other devices out
    }
}

static class JsonMerge
{
    /// Overlays the properties of <paramref name="extra"/> on <paramref name="baseObject"/>, for building request bodies in tests.
    public static Dictionary<string, object?> Merge(this object baseObject, object? extra)
    {
        var result = baseObject.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(baseObject));
        foreach (var p in extra?.GetType().GetProperties() ?? []) result[p.Name] = p.GetValue(extra);
        return result;
    }
}
