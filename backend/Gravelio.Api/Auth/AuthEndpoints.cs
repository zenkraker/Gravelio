using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Auth;

public record LoginRequest([Required, MaxLength(250)] string Username, [Required, MaxLength(128)] string Password);
public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string SessionId);

public static class AuthEndpoints
{
    public const string RefreshCookie = "gravelio_refresh_token";
    const string CookiePath = "/api/v1/auth";
    // Precomputed so unknown usernames cost the same hashing time as wrong passwords.
    static readonly string DummyHash = new PasswordHasher<User>().HashPassword(null!, "not-a-real-password");

    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth");

        g.MapPost("/login", async (LoginRequest req, AppDb db, LoginLockout lockout, IPasswordHasher<User> hasher,
            TokenService tokens, AuthOptions options, HttpContext http) =>
        {
            if (lockout.Remaining(req.Username) is { } wait)
                return Results.Problem($"Account locked due to too many failed login attempts. Try again in {(int)wait.TotalSeconds} seconds.",
                    statusCode: StatusCodes.Status429TooManyRequests);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username.ToLower());
            var verified = hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, req.Password);
            if (user is null || verified == PasswordVerificationResult.Failed)
            {
                lockout.RecordFailure(req.Username);
                return Results.Problem("Incorrect username or password", statusCode: StatusCodes.Status401Unauthorized);
            }
            if (!user.Active) return Results.Problem("Inactive user", statusCode: StatusCodes.Status403Forbidden);
            lockout.Reset(req.Username);
            if (verified == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, req.Password);

            var session = new UserSession
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                RefreshTokenHash = "",
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(ua.Length, 512)] : null,
                ExpiresAt = DateTime.UtcNow.AddDays(options.RefreshTokenDays),
            };
            var refresh = TokenService.NewRefreshToken(session.Id);
            session.RefreshTokenHash = TokenService.Hash(refresh);
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            return Issue(http, tokens, options, user, session, refresh);
        }).RequireRateLimiting("sensitive");

        // The required X-Client-Type header can't be sent cross-site without a CORS preflight,
        // which together with the SameSite=Strict cookie blocks CSRF on refresh/logout.
        g.MapPost("/refresh", async (HttpContext http, AppDb db, TokenService tokens, AuthOptions options) =>
        {
            if (http.Request.Headers["X-Client-Type"] != "web") return Results.Problem("Missing X-Client-Type header", statusCode: 400);
            var token = http.Request.Cookies[RefreshCookie];
            var sessionId = token?.Split('.')[0];
            var session = sessionId is null ? null : await db.Sessions.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return Unauthorized(http, options);
            if (!TokenService.HashEquals(session.RefreshTokenHash, TokenService.Hash(token!)) || session.ExpiresAt < DateTime.UtcNow || !session.User.Active)
            {
                // A stale (already rotated) refresh token means it leaked: revoke the whole session.
                db.Sessions.Remove(session);
                await db.SaveChangesAsync();
                return Unauthorized(http, options);
            }
            var refresh = TokenService.NewRefreshToken(session.Id);
            session.RefreshTokenHash = TokenService.Hash(refresh);
            session.RotationCount++;
            session.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Issue(http, tokens, options, session.User, session, refresh);
        }).RequireRateLimiting("sensitive");

        g.MapPost("/logout", async (HttpContext http, AppDb db, AuthOptions options) =>
        {
            if (http.Request.Headers["X-Client-Type"] != "web") return Results.Problem("Missing X-Client-Type header", statusCode: 400);
            var sessionId = http.Request.Cookies[RefreshCookie]?.Split('.')[0];
            if (sessionId is not null) await db.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
            http.Response.Cookies.Delete(RefreshCookie, CookieOptions(options, DateTime.UnixEpoch));
            return Results.NoContent();
        });
    }

    static IResult Issue(HttpContext http, TokenService tokens, AuthOptions options, User user, UserSession session, string refresh)
    {
        http.Response.Cookies.Append(RefreshCookie, refresh, CookieOptions(options, session.ExpiresAt));
        var (access, expiresIn) = tokens.CreateAccessToken(user, session.Id);
        return Results.Ok(new TokenResponse(access, "bearer", expiresIn, session.Id));
    }

    static IResult Unauthorized(HttpContext http, AuthOptions options)
    {
        http.Response.Cookies.Delete(RefreshCookie, CookieOptions(options, DateTime.UnixEpoch));
        return Results.Problem("Invalid or expired session", statusCode: StatusCodes.Status401Unauthorized);
    }

    static CookieOptions CookieOptions(AuthOptions options, DateTime expires) => new()
    {
        HttpOnly = true,
        Secure = options.SecureCookies,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expires,
    };
}
