using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;
using Gravelio.Api.Geocoding;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Gravelio.Tests;

[Collection("api")]
public class GeocodingTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    sealed class FakeNominatim(HttpStatusCode status) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpStatusCode Status { get; set; } = status;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent("""{"address":{"city":"Lisboa","village":"Alfama","country":"Portugal"}}"""),
            });
        }
    }

    (Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory, GeocodingJob Job) JobWith(FakeNominatim fake)
    {
        var factory = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Geocoding:MinIntervalMs", "0");
            b.UseSetting("Geocoding:BatchSize", "500");
            b.ConfigureTestServices(s => s.AddHttpClient(GeocodingJob.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        return (factory, factory.Services.GetRequiredService<GeocodingJob>());
    }

    async Task<(ApiClient Owner, int Id)> UploadAsync(string username, int visibility)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        var owner = await api.LoginAsync(username, Password);
        var upload = await owner.Http.PostAsync("/api/v1/activities/upload",
            TestData.Upload("geo.gpx", TestData.Gpx("Geo", new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc).AddDays(Math.Abs(username.GetHashCode()) % 300))));
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync($"/api/v1/activities/{id}",
            new { Name = "Geo", ActivityType = 1, Visibility = visibility }, ApiClient.Json)).StatusCode);
        return (owner, id);
    }

    [Fact]
    public async Task The_job_names_the_place_of_an_activity_and_asks_politely()
    {
        var (owner, id) = await UploadAsync("geo.owner", Visibilities.Public);
        var fake = new FakeNominatim(HttpStatusCode.OK);
        var (factory, job) = JobWith(fake);
        using var _ = factory;
        await job.RunOnceAsync(CancellationToken.None);

        var asked = fake.Requests.First(r => r.RequestUri!.Query.Contains("lat=38.7223&lon=-9.1393"));
        Assert.StartsWith("https://nominatim.openstreetmap.org/reverse", asked.RequestUri!.ToString());
        Assert.Contains("Gravelio", asked.Headers.UserAgent.ToString()); // the public server wants to know who is calling
        var activity = await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}");
        Assert.Equal("Lisboa", activity.GetProperty("city").GetString());
        Assert.Equal("Alfama", activity.GetProperty("town").GetString());
        Assert.Equal("Portugal", activity.GetProperty("country").GetString());

        // Done once: another pass does not ask again.
        var before = fake.Requests.Count;
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(before, fake.Requests.Count);
    }

    [Fact]
    public async Task Hiding_the_location_hides_the_place_name_from_everyone_else()
    {
        var (owner, id) = await UploadAsync("geo.hidden", Visibilities.Public);
        var (factory, job) = JobWith(new FakeNominatim(HttpStatusCode.OK));
        using var _ = factory;
        await job.RunOnceAsync(CancellationToken.None);

        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "geo.viewer", Password);
        var viewer = await api.LoginAsync("geo.viewer", Password);
        Assert.Equal("Portugal", (await viewer.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("country").GetString());

        // Hiding the map hides it too, and the owner still sees it.
        foreach (var flag in new[] { "HideActivityLocation", "HideActivityMap" })
        {
            var privacy = new Dictionary<string, object> { ["DefaultActivityVisibility"] = Visibilities.Public, [flag] = true };
            Assert.Equal(HttpStatusCode.OK, (await owner.Http.PutAsJsonAsync("/api/v1/users/me/privacy", privacy, ApiClient.Json)).StatusCode);
            var seen = await viewer.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}");
            Assert.All(new[] { "city", "town", "country" }, f => Assert.Equal(JsonValueKind.Null, seen.GetProperty(f).ValueKind));
            Assert.Equal("Portugal", (await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("country").GetString());
        }
    }

    [Fact]
    public async Task A_failed_lookup_is_retried_after_a_day_not_on_every_pass()
    {
        var (owner, id) = await UploadAsync("geo.retry", Visibilities.Private);
        var fake = new FakeNominatim(HttpStatusCode.ServiceUnavailable);
        var (factory, job) = JobWith(fake);
        using var _ = factory;
        await job.RunOnceAsync(CancellationToken.None);
        var tried = fake.Requests.Count;
        Assert.True(tried > 0);
        Assert.Equal(JsonValueKind.Null, (await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("country").ValueKind);

        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(tried, fake.Requests.Count); // not again today

        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDb>().ActivityLocationAttempts
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AttemptedAt, DateTime.UtcNow.AddDays(-2)));
        fake.Status = HttpStatusCode.OK;
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal("Portugal", (await owner.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("country").GetString());
    }

    [Fact]
    public async Task Only_one_replica_runs_the_job_at_a_time()
    {
        await UploadAsync("geo.lock", Visibilities.Private);
        var fake = new FakeNominatim(HttpStatusCode.OK);
        var (factory, job) = JobWith(fake);
        using var _ = factory;

        // Another "replica" holds the job's lock on its own connection.
        string connectionString;
        using (var scope = api.Services.CreateScope()) connectionString = scope.ServiceProvider.GetRequiredService<AppDb>().Database.GetConnectionString()!;
        await using var other = new NpgsqlConnection(connectionString);
        await other.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"select pg_advisory_lock({GeocodingJob.Lock})", other)) await cmd.ExecuteNonQueryAsync();
        await job.RunOnceAsync(CancellationToken.None);
        Assert.Empty(fake.Requests);

        await using (var cmd = new NpgsqlCommand($"select pg_advisory_unlock({GeocodingJob.Lock})", other)) await cmd.ExecuteNonQueryAsync();
        await job.RunOnceAsync(CancellationToken.None);
        Assert.NotEmpty(fake.Requests);
    }
}
