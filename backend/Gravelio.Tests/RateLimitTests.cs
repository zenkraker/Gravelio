using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace Gravelio.Tests;

/// Each tier has its own limit and answers 429 as problem details; the test server shares one in-process client IP, so limits are set to 2.
[Collection("api")]
public class RateLimitTests(ApiFixture api)
{
    static void Limit(IWebHostBuilder b, string name, int permits) => b.UseSetting($"RateLimits:{name}", permits.ToString());

    static async Task AssertThenLimited(Func<Task<HttpResponseMessage>> call, HttpStatusCode ok)
    {
        for (int i = 0; i < 2; i++) Assert.Equal(ok, (await call()).StatusCode);
        var limited = await call();
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Reads_are_limited_per_user()
    {
        using var factory = api.WithWebHostBuilder(b => Limit(b, "ReadsPerMinute", 3)); // the XSRF sync below is the first read
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();
        await AssertThenLimited(() => client.Http.GetAsync("/api/v1/activities/types"), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Writes_are_counted_apart_from_reads()
    {
        using var factory = api.WithWebHostBuilder(b => Limit(b, "WritesPerMinute", 2));
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();
        // Unknown username: a 401 each time, so no account lockout gets involved.
        await AssertThenLimited(() => client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = "nobody", Password = "x" }, ApiClient.Json), HttpStatusCode.Unauthorized);
        for (int i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync("/api/v1/activities/types")).StatusCode);
    }

    [Fact]
    public async Task Uploads_have_their_own_limit()
    {
        using var factory = api.WithWebHostBuilder(b => Limit(b, "UploadsPerMinute", 2));
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();
        // Not signed in: 401 from the endpoint while the limiter still counts the call.
        await AssertThenLimited(() => client.Http.PostAsync("/api/v1/activities/upload", new MultipartFormDataContent()), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task One_user_does_not_use_up_another_users_limit()
    {
        var admin = await api.LoginAsync();
        var name = $"rl{Guid.NewGuid():N}"[..12];
        await api.CreateUserAsync(admin, name);
        using var factory = api.WithWebHostBuilder(b => Limit(b, "ReadsPerMinute", 2));
        var a = new ApiClient(factory.CreateClient());
        var b2 = new ApiClient(factory.CreateClient());
        foreach (var (c, u, p) in new[] { (a, "admin", ApiFixture.AdminPassword), (b2, name, "Str0ng-pass!") })
        {
            await c.SyncXsrfAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await c.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = u, Password = p }, ApiClient.Json)).StatusCode);
        }
        for (int i = 0; i < 2; i++) await a.Http.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await a.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b2.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }
}
