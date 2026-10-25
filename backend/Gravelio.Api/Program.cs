using System.Text.Json;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
// Forwarded headers (client IP, scheme) are believed only from these networks: anyone else could forge their address
// and walk around the per-IP login limit. The defaults are the private ranges Docker and typical proxies use.
string[] trustedProxies = builder.Configuration.GetSection("TrustedProxies").Get<string[]>()
    ?? ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7", "127.0.0.0/8", "::1/128"];
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    foreach (var network in trustedProxies) o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
// Auth cookies are encrypted with these keys. They live on their own volume, not in the database, so a database dump can't forge sessions.
builder.Services.AddDataProtection().SetApplicationName("Gravelio")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "keys"));
builder.Services.AddGravelioAuth();
builder.Services.AddFastEndpoints().SwaggerDocument(o => o.DocumentSettings = s => s.Title = "Gravelio API");
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = UploadActivityEndpoint.MaxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = UploadActivityEndpoint.MaxUploadBytes + 1024 * 1024);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("sensitive", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginAttemptsPerMinute,
            Window = TimeSpan.FromMinutes(1),
        }));
});

// Telemetry is always collected; it only leaves the process when an OTLP endpoint is configured.
builder.Logging.AddOpenTelemetry(o => o.IncludeFormattedMessage = true);
var telemetry = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsqlInstrumentation());
if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) telemetry.UseOtlpExporter();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Production applies an EF migration bundle before the app starts.
    if (app.Environment.IsDevelopment()) await scope.ServiceProvider.GetRequiredService<AppDb>().Database.MigrateAsync();
    await SeedAdmin(scope.ServiceProvider, app.Logger);
}

app.UseForwardedHeaders();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
// Before authorization, which short-circuits 401s: those responses must still hand out an XSRF token.
app.UseXsrfProtection();
app.UseAuthorization();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Serializer.Options.DictionaryKeyPolicy = null;
    c.Errors.UseProblemDetails();
}).UseSwaggerGen();

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Creates the first admin on an empty database; the password comes from config or is generated and logged once.
static async Task SeedAdmin(IServiceProvider services, ILogger logger)
{
    var users = services.GetRequiredService<UserManager<User>>();
    if (users.Users.Any()) return;

    var config = services.GetRequiredService<IOptions<AdminOptions>>().Value;
    // Never generated: a generated password would have to be logged, and logs outlive first login.
    var password = config.Password ?? throw new InvalidOperationException("Set Admin__Password (12+ characters) to create the first admin user.");
    if (password.Length < AdminPasswordValidator.AdminMinLength) throw new InvalidOperationException($"Admin:Password: {AdminPasswordValidator.TooShortMessage}");

    await services.GetRequiredService<RoleManager<IdentityRole<int>>>().CreateAsync(new IdentityRole<int>(AppRoles.Admin));
    var admin = new User
    {
        Name = "Administrator",
        UserName = "admin",
        Email = config.Email ?? "admin@example.com",
        CreatedAt = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
    };
    var result = await users.CreateAsync(admin, password);
    if (result.Succeeded) result = await users.AddToRoleAsync(admin, AppRoles.Admin);
    if (!result.Succeeded) throw new InvalidOperationException("Could not create the initial admin: " + string.Join(" ", result.Errors.Select(e => e.Description)));

    logger.LogInformation("Created initial user 'admin'.");
}
