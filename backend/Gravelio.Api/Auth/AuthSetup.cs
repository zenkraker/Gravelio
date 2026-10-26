using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Gravelio.Api.ApiKeys;
using Gravelio.Api.Data;
using Gravelio.Api.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Auth;

public static class AppRoles
{
    public const string Admin = "admin";
}

public sealed class AuthOptions
{
    public bool SecureCookies { get; set; } = true;

    [Range(1, 90)]
    public int SessionDays { get; set; } = 7;

    /// Per client IP, for the login endpoint.
    [Range(1, 10_000)]
    public int LoginAttemptsPerMinute { get; set; } = 10;
}

/// Docker Compose passes unset variables as empty strings, so blank means "not set".
public sealed class AdminOptions
{
    [EmailAddress]
    public string? Email
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string? Password
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// Identity's password rules apply to everyone; admins additionally need a longer password.
public sealed class AdminPasswordValidator : IPasswordValidator<User>
{
    public const int AdminMinLength = 12;

    public static string TooShortMessage => $"Admin passwords must be at least {AdminMinLength} characters long.";

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password) =>
        password is { Length: < AdminMinLength } && user.Id != 0 && await manager.IsInRoleAsync(user, AppRoles.Admin)
            ? IdentityResult.Failed(new IdentityError { Code = "PasswordTooShortForAdmin", Description = TooShortMessage })
            : IdentityResult.Success;
}

public static class ClaimsPrincipalExtensions
{
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public static class AuthSetup
{
    public const string AuthCookie = "gravelio_auth";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    public static IServiceCollection AddGravelioAuth(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration("Auth").ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AdminOptions>().BindConfiguration("Admin").ValidateDataAnnotations().ValidateOnStart();

        services.AddIdentityCore<User>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = o.Password.RequireLowercase = o.Password.RequireUppercase = o.Password.RequireNonAlphanumeric = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDb>()
            .AddSignInManager()
            .AddPasswordValidator<AdminPasswordValidator>()
            // Needed to generate the short-lived Identity token behind a password reset.
            .AddDefaultTokenProviders();
        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies();
        authentication.AddScheme<AuthenticationSchemeOptions, ApiKeyHandler>(ApiKeyAuth.Scheme, null);
        services.AddAuthorization();

        // Revoke cookies on password, role or deactivation changes at the next request, not after the default 30 minutes.
        // The validator rebuilds the principal from the user each time, so carry the session id claim over to the new one.
        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.Zero;
            o.OnRefreshingPrincipal = ctx =>
            {
                if (ctx.CurrentPrincipal?.FindFirst(SessionTicketStore.SessionIdClaim) is { } sid) ctx.NewPrincipal?.Identities.First().AddClaim(sid);
                return Task.CompletedTask;
            };
        });

        services.AddHttpContextAccessor();
        services.AddSingleton<SessionTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AuthOptions>, SessionTicketStore>((o, auth, sessions) =>
            {
                // The cookie holds a key into UserSessions, so a person can see and end each device's session.
                o.SessionStore = sessions;
                o.Cookie.Name = AuthCookie;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = auth.Value.SecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromDays(auth.Value.SessionDays);
                o.SlidingExpiration = true;
                // An API answers with status codes; the SPA owns the login page.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            });

        services.AddAntiforgery(o => o.HeaderName = XsrfHeader);
        return services;
    }

    /// Angular's built-in XSRF support echoes the XSRF-TOKEN cookie back as X-XSRF-TOKEN. Safe requests hand out a token
    /// bound to the current user; unsafe ones must present it. Login therefore needs a GET first, and a fresh GET after it.
    public static IApplicationBuilder UseXsrfProtection(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
        if (!ctx.Request.Path.StartsWithSegments("/api")) { await next(ctx); return; }

        if (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method))
        {
            var tokens = antiforgery.GetAndStoreTokens(ctx);
            ctx.Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false, // the SPA must read it
                SameSite = SameSiteMode.Strict,
                Secure = ctx.Request.IsHttps,
            });
        }
        // An uploader with a key and no cookie has no browser session to forge; a request that has the cookie must still prove itself.
        else if (!(AcceptsKeyInsteadOfToken(ctx)) && !await antiforgery.IsRequestValidAsync(ctx))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        await next(ctx);
    });

    static bool AcceptsKeyInsteadOfToken(HttpContext ctx) =>
        ctx.GetEndpoint()?.Metadata.GetMetadata<AllowsApiKey>() is not null
        && ctx.Request.Headers.ContainsKey(ApiKeyAuth.Header)
        && !ctx.Request.Cookies.ContainsKey(AuthCookie);
}
