using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Gravelio.Api.Activities;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
if (authOptions.JwtSecret.Length < 32)
    throw new InvalidOperationException("Auth:JwtSecret must be set to at least 32 characters (env: Auth__JwtSecret).");

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<LoginLockout>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = ActivityEndpoints.MaxUploadBytes);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = ActivityEndpoints.MaxUploadBytes + 1024 * 1024);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    var key = new TokenService(authOptions).Key;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = TokenService.Issuer,
        ValidAudience = TokenService.Issuer,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    // Logout, password changes and deactivation take effect immediately, not when the access token expires.
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var sid = ctx.Principal?.FindFirst(TokenService.SessionClaim)?.Value;
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDb>();
            if (sid is null || !await db.Sessions.AnyAsync(s => s.Id == sid && s.User.Active && s.ExpiresAt > DateTime.UtcNow))
                ctx.Fail("Session revoked");
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("sensitive", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    await SeedAdmin(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Configuration, app.Logger);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();
app.MapUsers();
app.MapActivities();
app.MapFallbackToFile("index.html");

app.Run();

// Creates the first admin on an empty database; the password comes from config or is generated and logged once.
static async Task SeedAdmin(AppDb db, IPasswordHasher<User> hasher, IConfiguration config, ILogger logger)
{
    if (await db.Users.AnyAsync()) return;
    var password = config["Admin:Password"];
    bool generated = string.IsNullOrEmpty(password);
    if (generated) password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)) + "aA1!";
    else if (PasswordPolicy.Validate(password!, AccessTypes.Admin) is { } error)
        throw new InvalidOperationException($"Admin:Password is not strong enough: {error}");
    var admin = new User { Name = "Administrator", Username = "admin", Email = config["Admin:Email"] ?? "admin@example.com", AccessType = AccessTypes.Admin, PasswordHash = "" };
    admin.PasswordHash = hasher.HashPassword(admin, password!);
    db.Users.Add(admin);
    await db.SaveChangesAsync();
    if (generated) logger.LogWarning("Created initial user 'admin' with generated password: {Password} — change it after first login.", password);
    else logger.LogInformation("Created initial user 'admin'.");
}

public partial class Program;
