using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Gravelio.Api.Auth;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Common;

/// Requests per minute. Reads and writes are counted apart so a busy dashboard cannot starve a save.
public sealed class RateLimitOptions
{
    [Range(1, 100_000)]
    public int ReadsPerMinute { get; set; } = 120;

    [Range(1, 100_000)]
    public int WritesPerMinute { get; set; } = 30;

    [Range(1, 100_000)]
    public int UploadsPerMinute { get; set; } = 20;
}

public static class RateLimiting
{
    /// Policy names for <c>RequireRateLimiting</c>. <see cref="Sensitive"/> is per client IP (login, MFA, reset, sign-up, export/import);
    /// <see cref="Upload"/> is per user (per IP without a session) and stacks on the global read/write limit.
    public const string Sensitive = "sensitive";
    public const string Upload = "upload";

    /// https://learn.microsoft.com/aspnet/core/performance/rate-limit
    public static IServiceCollection AddGravelioRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>().BindConfiguration("RateLimits").ValidateDataAnnotations().ValidateOnStart();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Every route: a read bucket and a write bucket per signed-in user, or per client IP without a session.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var limits = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                var write = !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method);
                return RateLimitPartition.GetFixedWindowLimiter(
                    (write ? "w:" : "r:") + Caller(http),
                    _ => Window(write ? limits.WritesPerMinute : limits.ReadsPerMinute));
            });

            o.AddPolicy(Sensitive, http => RateLimitPartition.GetFixedWindowLimiter(
                Ip(http),
                _ => Window(http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginAttemptsPerMinute)));

            o.AddPolicy(Upload, http => RateLimitPartition.GetFixedWindowLimiter(
                Caller(http),
                _ => Window(http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value.UploadsPerMinute)));
        });
        return services;
    }

    static FixedWindowRateLimiterOptions Window(int permits) => new() { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) };

    static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Needs UseAuthentication before UseRateLimiter. An API-key request is only authenticated later, by its endpoint, so it counts by IP.
    static string Caller(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? "u:" + id : "ip:" + Ip(http);
}
