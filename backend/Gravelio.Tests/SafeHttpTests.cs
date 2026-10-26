using System.Net;
using System.Net.Sockets;
using System.Text;
using Gravelio.Api.Common;
using Gravelio.Api.Geocoding;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

[Collection("api")]
public class SafeHttpTests(ApiFixture api)
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("100.128.0.1", true)]
    [InlineData("2606:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)] // cloud metadata
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("::1", false)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("::ffff:10.0.0.1", false)] // an IPv4 address written as IPv6
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("2002:a00:1::1", false)] // 6to4 wrapping 10.0.0.1
    public void Only_addresses_on_the_public_internet_are_public(string address, bool expected) =>
        Assert.Equal(expected, SafeHttp.IsPublic(IPAddress.Parse(address)));

    /// A server on the loopback address that answers every request with the given raw response and counts the connections it gets.
    sealed class Listener : IDisposable
    {
        readonly TcpListener tcp = new(IPAddress.Loopback, 0);
        public int Connections;
        public int Port => ((IPEndPoint)tcp.LocalEndpoint).Port;

        public Listener(string response)
        {
            tcp.Start();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient client;
                    try
                    {
                        client = await tcp.AcceptTcpClientAsync();
                    }
                    catch
                    {
                        return;
                    }
                    Interlocked.Increment(ref Connections);
                    _ = Task.Run(async () =>
                    {
                        using var c = client;
                        try
                        {
                            var stream = c.GetStream();
                            // Read the whole request head before answering, and let the client close first: closing early would reset the connection.
                            var head = new StringBuilder();
                            var buffer = new byte[1024];
                            while (!head.ToString().Contains("\r\n\r\n"))
                            {
                                var read = await stream.ReadAsync(buffer);
                                if (read == 0) return;
                                head.Append(Encoding.ASCII.GetString(buffer, 0, read));
                            }
                            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
                            c.Client.Shutdown(SocketShutdown.Send);
                            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            while (await stream.ReadAsync(buffer, wait.Token) > 0) { }
                        }
                        catch
                        {
                            // The test has finished with this connection.
                        }
                    });
                }
            });
        }

        public void Dispose() => tcp.Stop();
    }

    static HttpClient Client(WebApplicationFactoryHolder factory) => factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(GeocodingJob.HttpClientName);

    sealed record WebApplicationFactoryHolder(IServiceProvider Services);

    [Fact]
    public async Task A_name_that_resolves_to_a_private_address_is_never_connected_to()
    {
        using var server = new Listener("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        var client = Client(new(api.Services));
        // https so the scheme check passes; "localhost" resolves to the loopback address, which the connect step must refuse.
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"https://localhost:{server.Port}/"));
        Assert.Contains("public address", error.ToString());
        Assert.Equal(0, server.Connections);
        // An address typed in as a literal is checked the same way.
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"https://127.0.0.1:{server.Port}/"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://169.254.169.254/latest/meta-data/"));
        Assert.Equal(0, server.Connections);
    }

    [Fact]
    public async Task Plain_http_is_refused_unless_private_networks_are_allowed()
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Client(new(api.Services)).GetAsync("http://example.com/"));
        Assert.Contains("https", error.Message);
    }

    [Fact]
    public async Task With_private_networks_allowed_a_lan_server_works_and_redirects_are_still_not_followed()
    {
        using var ok = new Listener("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        using var redirect = new Listener($"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{ok.Port}/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var factory = api.WithWebHostBuilder(b => b.UseSetting("Outbound:AllowPrivateNetworks", "true"));
        var client = Client(new(factory.Services));

        Assert.Equal("ok", await client.GetStringAsync($"http://127.0.0.1:{ok.Port}/"));
        var bounced = await client.GetAsync($"http://127.0.0.1:{redirect.Port}/");
        Assert.Equal(HttpStatusCode.Found, bounced.StatusCode);
        Assert.Equal(1, ok.Connections); // only the direct call: the redirect was not followed
    }
}
