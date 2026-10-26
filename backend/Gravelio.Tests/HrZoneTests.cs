using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Activities;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class HrZoneTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    [Fact]
    public void Samples_are_counted_into_five_zones_by_share_of_max()
    {
        // Max 200: limits 120, 140, 160, 180.
        var zones = HrZones.Compute([100, 119, 120, 139, 140, 159, 160, 179, 180, 199], 200, 1000, showRange: true);
        Assert.Equal([20, 20, 20, 20, 20], zones.Select(z => z.Percent));
        Assert.Equal([200, 200, 200, 200, 200], zones.Select(z => z.Seconds));
        Assert.Equal(["< 120", "120 - 139", "140 - 159", "160 - 179", ">= 180"], zones.Select(z => z.Range));
        Assert.All(HrZones.Compute([100], 200, 10, showRange: false), z => Assert.Null(z.Range));
        Assert.Empty(HrZones.Compute([], 200, 10, true));
    }

    [Theory]
    [InlineData(185, "1990-06-15", "2026-06-14", 185)] // a stored value wins
    [InlineData(null, "1990-06-15", "2026-06-14", 185)] // 35 years old: the birthday is tomorrow
    [InlineData(null, "1990-06-15", "2026-06-15", 184)] // 36 on the day
    [InlineData(null, null, "2026-06-15", null)]
    public void Max_heart_rate_comes_from_the_setting_or_the_age(int? stored, string? born, string today, int? expected) =>
        Assert.Equal(expected, HrZones.ResolveMax(stored, born is null ? null : DateOnly.Parse(born), DateOnly.Parse(today)));

    async Task<(ApiClient Owner, int Id)> OwnerWithMaxAsync(string username, int? max)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        var owner = await api.LoginAsync(username, Password);
        var profile = new { Name = "Zone Owner", Email = $"{username}@example.com", PreferredLanguage = "en", Gender = "unspecified", Units = "metric", MaxHeartRate = max, FirstDayOfWeek = "monday", Timezone = "UTC", Currency = "euro" };
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me", profile, ApiClient.Json)).StatusCode);
        var start = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc).AddDays(Math.Abs(username.GetHashCode()) % 200);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("z.gpx", TestData.Gpx("Zones", start, heartRate: true)));
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Zones", ActivityType = activity.GetProperty("activity_type").GetInt32(), Visibility = Visibilities.Public }, ApiClient.Json)).StatusCode);
        return (owner, id);
    }

    [Fact]
    public async Task The_owner_sees_zone_limits_and_other_viewers_see_only_the_split()
    {
        // The test track has heart rates 130-144: with a max of 200, 130-139 is zone 2 and 140-144 zone 3.
        var (owner, id) = await OwnerWithMaxAsync("zones.owner", 200);
        var own = await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/hr-zones");
        Assert.Equal(5, own.Length);
        Assert.Equal(0, own[0].GetProperty("percent").GetDouble());
        Assert.True(own[1].GetProperty("percent").GetDouble() > 0 && own[2].GetProperty("percent").GetDouble() > 0);
        Assert.Equal("120 - 139", own[1].GetProperty("range").GetString());

        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "zones.viewer", Password);
        var viewer = await api.LoginAsync("zones.viewer", Password);
        var seen = await viewer.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/hr-zones");
        Assert.Equal(own.Select(z => z.GetProperty("percent").GetDouble()), seen.Select(z => z.GetProperty("percent").GetDouble()));
        // The limits are a fixed share of the owner's max heart rate, so they are not shown to anyone else.
        Assert.All(seen, z => Assert.Equal(JsonValueKind.Null, z.GetProperty("range").ValueKind));

        // A hidden heart rate leaves nothing to count.
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy",
            new { DefaultActivityVisibility = Visibilities.Public, HideActivityHr = true }, ApiClient.Json)).StatusCode);
        Assert.Empty(await viewer.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/hr-zones"));
        Assert.Equal(5, (await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/hr-zones")).Length);
    }

    [Fact]
    public async Task Without_a_max_heart_rate_or_a_birthdate_there_are_no_zones()
    {
        var (owner, id) = await OwnerWithMaxAsync("zones.nomax", null);
        Assert.Empty(await owner.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{id}/hr-zones"));
    }

    [Fact]
    public async Task Another_users_private_activity_has_no_zones_to_read()
    {
        var (owner, id) = await OwnerWithMaxAsync("zones.private", 200);
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Zones", ActivityType = 1, Visibility = Visibilities.Private }, ApiClient.Json)).StatusCode);
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "zones.nosy", Password);
        var nosy = await api.LoginAsync("zones.nosy", Password);
        Assert.Equal(HttpStatusCode.NotFound, (await nosy.Http.GetAsync($"/api/v1/activities/{id}/hr-zones")).StatusCode);
    }
}
