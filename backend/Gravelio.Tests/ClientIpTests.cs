using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

/// The per-IP login limit must follow the real client, and a stranger must not be able to pick their own "IP".
[Collection("api")]
public class ClientIpTests(ApiFixture api)
{
    // Three logins a minute per IP; unknown usernames, so no account lockout gets involved.
    static void LowLimit(IWebHostBuilder b) => b.UseSetting("Auth:LoginAttemptsPerMinute", "3");

    /// Pretends every request arrives from the given peer address, as a TCP connection from that machine would.
    sealed class PeerAddress(IPAddress peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, nextMiddleware) =>
            {
                ctx.Connection.RemoteIpAddress = peer;
                return nextMiddleware();
            });
            next(app);
        };
    }

    static async Task<HttpStatusCode> LoginAsync(ApiClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { Username = "nobody", Password = "x" }, options: ApiClient.Json),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.Http.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task A_stranger_cannot_dodge_the_login_limit_by_forging_a_forwarded_address()
    {
        // A peer on a public address is not a trusted proxy, so its X-Forwarded-For must be ignored.
        // (A peer with no address, such as a Unix socket, is trusted: that is how ASP.NET Core treats it.)
        using var factory = api.WithWebHostBuilder(b => LowLimit(b.ConfigureServices(s => s.AddTransient<IStartupFilter>(_ => new PeerAddress(IPAddress.Parse("203.0.113.50"))))));
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();

        for (int i = 1; i <= 3; i++) Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(client, $"203.0.113.{i}"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAsync(client, "203.0.113.99"));
    }

    [Fact]
    public async Task Behind_a_trusted_proxy_each_real_client_gets_its_own_login_limit()
    {
        using var factory = api.WithWebHostBuilder(b => LowLimit(b.ConfigureServices(s => s.AddTransient<IStartupFilter>(_ => new PeerAddress(IPAddress.Parse("172.18.0.2"))))));
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();

        for (int i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(client, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginAsync(client, "198.51.100.1")); // client A used its three
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(client, "198.51.100.2")); // client B is unaffected
    }
}
