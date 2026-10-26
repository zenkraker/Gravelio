using System.Text.Json;
using FastEndpoints;
using FastEndpoints.Swagger;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Gravelio.Api.Geocoding;
using Gravelio.Api.Imports;
using Gravelio.Api.Sso;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
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
// Email is optional: an unconfigured server logs instead of failing, so the app runs without SMTP.
builder.Services.AddOptions<EmailOptions>().BindConfiguration("Email").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddFastEndpoints().SwaggerDocument(o => o.DocumentSettings = s => s.Title = "Gravelio API");
// Every error body, including unhandled exceptions and bare 401/404/429 responses, is ASP.NET Core's own problem details.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddGravelioRateLimiting();
builder.Services.AddGravelioSso();
// Background jobs. Geocoding is opt-in because it sends activity positions to another server.
builder.Services.AddOptions<GeocodingOptions>().BindConfiguration("Geocoding").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddGuardedHttpClient(GeocodingJob.HttpClientName);
builder.Services.AddOptions<ImportOptions>().BindConfiguration("ImportJobs").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<BulkImportJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BulkImportJob>());
builder.Services.AddSingleton<GeocodingJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GeocodingJob>());

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
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
// Authenticated API responses must never be reused from the browser cache.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
// After authentication, so the global limit can count per user.
app.UseRateLimiter();
// Before authorization, which short-circuits 401s: those responses must still hand out an XSRF token.
app.UseXsrfProtection();
app.UseAuthorization();
app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api/v1";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Serializer.Options.DictionaryKeyPolicy = null;
    // Validation and ThrowError failures leave as ASP.NET Core's HttpValidationProblemDetails, not FastEndpoints' own error type.
    c.Errors.ResponseBuilder = (failures, ctx, status) => new HttpValidationProblemDetails(failures
        .GroupBy(f => JsonNamingPolicy.SnakeCaseLower.ConvertName(f.PropertyName))
        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()))
    {
        Status = status,
        Title = ReasonPhrases.GetReasonPhrase(status),
        Instance = ctx.Request.Path,
        Extensions = { ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? ctx.TraceIdentifier },
    };
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
        EmailConfirmed = true,
        CreatedAt = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
    };
    var result = await users.CreateAsync(admin, password);
    if (result.Succeeded) result = await users.AddToRoleAsync(admin, AppRoles.Admin);
    if (!result.Succeeded) throw new InvalidOperationException("Could not create the initial admin: " + string.Join(" ", result.Errors.Select(e => e.Description)));

    logger.LogInformation("Created initial user 'admin'.");
}
