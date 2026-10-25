using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
// Auth cookies are encrypted with these keys; keeping them in the database survives container restarts.
builder.Services.AddDataProtection().SetApplicationName("Gravelio").PersistKeysToDbContext<AppDb>();
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

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseXsrfProtection();
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
    var password = config.Password;
    bool generated = string.IsNullOrEmpty(password);
    if (generated) password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)) + "aA1!";
    else if (password!.Length < AdminPasswordValidator.AdminMinLength) throw new InvalidOperationException($"Admin:Password: {AdminPasswordValidator.TooShortMessage}");

    await services.GetRequiredService<RoleManager<IdentityRole<int>>>().CreateAsync(new IdentityRole<int>(AppRoles.Admin));
    var admin = new User
    {
        Name = "Administrator",
        UserName = "admin",
        Email = config.Email ?? "admin@example.com",
        CreatedAt = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
    };
    var result = await users.CreateAsync(admin, password!);
    if (result.Succeeded) result = await users.AddToRoleAsync(admin, AppRoles.Admin);
    if (!result.Succeeded) throw new InvalidOperationException("Could not create the initial admin: " + string.Join(" ", result.Errors.Select(e => e.Description)));

    if (generated) logger.LogWarning("Created initial user 'admin' with generated password: {Password} — change it after first login.", password);
    else logger.LogInformation("Created initial user 'admin'.");
}
