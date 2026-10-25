using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Gravelio.Tests.TestData;

namespace Gravelio.Tests;

[Collection("api")]
public class ActivityStatsTests(ApiFixture api)
{
    /// A fresh user per test: statistics aggregate everything the user owns, so a shared user would see other tests' uploads.
    async Task<ApiClient> NewUserAsync()
    {
        var admin = await api.LoginAsync();
        var username = $"stats{Guid.NewGuid():N}"[..16];
        await api.CreateUserAsync(admin, username);
        return await api.LoginAsync(username, "Str0ng-pass!");
    }

    static Task<HttpResponseMessage> UploadAsync(ApiClient client, DateTime start, string sport = "running") =>
        client.Http.PostAsync("/api/v1/activities/upload", Upload("stats.gpx", Gpx("Stats run", start, sport: sport)));

    static async Task<JsonElement> StatsAsync(ApiClient client, string query)
    {
        var response = await client.Http.GetAsync($"/api/v1/activities/stats?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
    }

    static int Count(JsonElement stats) => stats.GetProperty("activity_count").GetInt32();

    static JsonElement Bucket(JsonElement stats, string start) =>
        stats.GetProperty("breakdown").EnumerateArray()
            .Single(b => b.GetProperty("start").GetString() == start);

    [Fact]
    public async Task Weekly_summary_groups_activities_by_local_day()
    {
        var user = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 4, 9, 0, 0, DateTimeKind.Utc))).StatusCode);

        var stats = await StatsAsync(user, "period=week&date=2026-03-04");
        Assert.Equal(2, Count(stats));
        Assert.Equal(7236, stats.GetProperty("total_distance").GetDouble());
        Assert.Equal(7, stats.GetProperty("breakdown").GetArrayLength());

        Assert.Equal(1, Bucket(stats, "2026-03-02").GetProperty("count").GetInt32());
        Assert.Equal(0, Bucket(stats, "2026-03-03").GetProperty("count").GetInt32());
        Assert.Equal(1, Bucket(stats, "2026-03-04").GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Weekly_summary_uses_the_activity_timezone_not_utc()
    {
        var user = await NewUserAsync();
        // 23:30 UTC on 1 July is already 00:30 on 2 July in Lisbon (summer time).
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 7, 1, 23, 30, 0, DateTimeKind.Utc))).StatusCode);

        var stats = await StatsAsync(user, "period=week&date=2026-07-02");
        Assert.Equal(1, Count(stats));
        Assert.Equal(0, Bucket(stats, "2026-06-29").GetProperty("count").GetInt32()); // Monday of that week
        Assert.Equal(0, Bucket(stats, "2026-07-01").GetProperty("count").GetInt32()); // the UTC date
        Assert.Equal(1, Bucket(stats, "2026-07-02").GetProperty("count").GetInt32()); // the local date
    }

    [Fact]
    public async Task Monthly_summary_covers_the_month_in_contiguous_weeks()
    {
        var user = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc))).StatusCode);

        var stats = await StatsAsync(user, "period=month&date=2026-03-20");
        Assert.Equal(2, Count(stats));
        var buckets = stats.GetProperty("breakdown").EnumerateArray().ToList();
        Assert.Equal("2026-03-01", buckets[0].GetProperty("start").GetString());
        Assert.Equal("2026-04-01", buckets[^1].GetProperty("end").GetString());
        for (var i = 1; i < buckets.Count; i++)
            Assert.Equal(buckets[i - 1].GetProperty("end").GetString(), buckets[i].GetProperty("start").GetString());
        Assert.Equal(2, buckets.Sum(b => b.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task Yearly_summary_has_one_bucket_per_month_and_lifetime_one_per_year()
    {
        var user = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))).StatusCode);

        var year = await StatsAsync(user, "period=year&year=2026");
        Assert.Equal(1, Count(year));
        var months = year.GetProperty("breakdown").EnumerateArray().ToList();
        Assert.Equal(12, months.Count);
        Assert.Equal("2026-03-01", months[2].GetProperty("start").GetString());
        Assert.Equal(1, months[2].GetProperty("count").GetInt32());

        var lifetime = await StatsAsync(user, "period=lifetime");
        Assert.Equal(1, Count(lifetime));
        var years = lifetime.GetProperty("breakdown").EnumerateArray().ToList();
        var only = Assert.Single(years);
        Assert.Equal("2026-01-01", only.GetProperty("start").GetString());
        Assert.Equal(1, only.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Type_breakdown_and_filter_by_type_name()
    {
        var user = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc), "ride")).StatusCode);

        var all = await StatsAsync(user, "period=lifetime");
        Assert.Equal(2, all.GetProperty("type_breakdown").GetArrayLength());
        Assert.Contains(all.GetProperty("type_breakdown").EnumerateArray(),
            t => t.GetProperty("name").GetString() == "Ride" && t.GetProperty("count").GetInt32() == 1);

        var runs = await StatsAsync(user, "period=lifetime&type=run");
        Assert.Equal(1, Count(runs));
        Assert.Equal("Run", Assert.Single(runs.GetProperty("type_breakdown").EnumerateArray()).GetProperty("name").GetString());

        // An unknown type name is an empty summary, not the whole history.
        Assert.Equal(0, Count(await StatsAsync(user, "period=lifetime&type=nonsense")));
    }

    [Fact]
    public async Task Stats_only_cover_the_caller_and_reject_bad_parameters()
    {
        var user = await NewUserAsync();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(user, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc))).StatusCode);
        Assert.Equal(1, Count(await StatsAsync(user, "period=lifetime")));

        var other = await NewUserAsync();
        Assert.Equal(0, Count(await StatsAsync(other, "period=lifetime")));

        var badPeriod = await other.Http.GetAsync("/api/v1/activities/stats?period=decade");
        Assert.Equal(HttpStatusCode.BadRequest, badPeriod.StatusCode);
        var badYear = await other.Http.GetAsync("/api/v1/activities/stats?period=year&year=1800");
        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
    }
}

