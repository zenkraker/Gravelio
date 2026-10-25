using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Gravelio.Tests;

/// The API under test, running against a real PostgreSQL started by Testcontainers.
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "Admin-Password-1!"; // from appsettings.Development.json

    readonly PostgreSqlContainer db = new PostgreSqlBuilder("postgres:16-alpine").Build();

    async Task IAsyncLifetime.InitializeAsync() => await db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", db.GetConnectionString());
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "10000"); // every test shares the in-process "client IP"
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "gravelio-test-keys"));
    }

    /// Logs in as the seeded admin, or as another user.
    public async Task<ApiClient> LoginAsync(string username = "admin", string password = AdminPassword)
    {
        var client = new ApiClient(CreateClient());
        await client.SyncXsrfAsync();
        var response = await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = username, Password = password }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await client.SyncXsrfAsync(); // the XSRF token is bound to the signed-in user
        return client;
    }

    /// Creates a regular user as the admin and returns its id.
    public async Task<int> CreateUserAsync(ApiClient admin, string username, string password = "Str0ng-pass!")
    {
        var response = await admin.Http.PostAsJsonAsync("/api/v1/users",
            new { Name = username, Username = username, Email = $"{username}@example.com", AccessType = "regular", Active = true, Password = password }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
    }
}

public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public HttpClient Http => http;

    /// Mirrors Angular's XSRF support: read the XSRF-TOKEN cookie and echo it as X-XSRF-TOKEN. The SPA gets its token
    /// from GET /users/me, which answers 401 while signed out, so that is where the tests get it too.
    public async Task SyncXsrfAsync()
    {
        using var response = await http.GetAsync("/api/v1/users/me");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN="));
        var token = Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';')]);
        http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        http.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }

    public Task<T> GetJsonAsync<T>(string url) => http.GetFromJsonAsync<T>(url, Json)!;
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
