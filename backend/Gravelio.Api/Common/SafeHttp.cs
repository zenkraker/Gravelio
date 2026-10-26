using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Common;

public sealed class OutboundOptions
{
    /// Off by default: a server that fetches URLs an admin typed in must not be a way to reach its own network
    /// (cloud metadata, databases, the router). Turn it on to use a service on the LAN, such as a self-hosted geocoder.
    public bool AllowPrivateNetworks { get; set; }
}

/// Every outbound HTTP call goes through a named client made here (<see cref="IHttpClientFactory"/>). Its connect step resolves the host
/// and refuses loopback, link-local, private and other non-public addresses, then connects to the address it checked, so a name that
/// resolves differently a moment later (DNS rebinding) cannot slip through. Redirects are not followed, so a public server cannot bounce a
/// request to an internal one, and plain http is refused unless private networks are allowed.
public static class SafeHttp
{
    public static IHttpClientBuilder AddGuardedHttpClient(this IServiceCollection services, string name, Action<HttpClient>? configure = null)
    {
        services.AddOptions<OutboundOptions>().BindConfiguration("Outbound").ValidateDataAnnotations().ValidateOnStart();
        var builder = configure is null ? services.AddHttpClient(name) : services.AddHttpClient(name, configure);
        return builder
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var allowPrivate = sp.GetRequiredService<IOptions<OutboundOptions>>().Value.AllowPrivateNetworks;
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = (context, ct) => ConnectAsync(context, allowPrivate, ct),
                };
            })
            .AddHttpMessageHandler(sp => new HttpsOnly(sp.GetRequiredService<IOptions<OutboundOptions>>().Value.AllowPrivateNetworks));
    }

    static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, bool allowPrivate, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = (await Dns.GetHostAddressesAsync(host, ct)).Where(a => allowPrivate || IsPublic(a)).ToArray();
        if (addresses.Length == 0) throw new HttpRequestException($"Refused to connect to {host}: it does not resolve to a public address.");
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    sealed class HttpsOnly(bool allowPrivate) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!allowPrivate && request.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new HttpRequestException("Refused: only https addresses may be called.");
            return base.SendAsync(request, ct);
        }
    }

    /// True for an address on the public internet.
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224                      // this network, private, loopback, multicast, reserved, broadcast
                     || b[0] == 100 && b[1] is >= 64 and <= 127                  // carrier-grade NAT
                     || b[0] == 169 && b[1] == 254                               // link-local, including the cloud metadata address
                     || b[0] == 172 && b[1] is >= 16 and <= 31
                     || b[0] == 192 && b[1] == 168
                     || b[0] == 192 && b[1] == 0 && b[2] == 0                    // IETF protocol assignments
                     || b[0] == 198 && b[1] is 18 or 19);                        // benchmarking
        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal || address.IsIPv6Teredo
                 || b[0] == 0x20 && b[1] == 0x02);                               // 6to4 can embed a private IPv4 address
    }
}
