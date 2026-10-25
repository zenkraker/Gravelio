using System.ComponentModel.DataAnnotations;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public record UserRead(int Id, string Name, string Username, string Email, string AccessType, bool Active, string? City,
    DateOnly? Birthdate, string PreferredLanguage, string Gender, string Units, int? Height, int? MaxHeartRate,
    string FirstDayOfWeek, string Timezone)
{
    public static UserRead From(User u) => new(u.Id, u.Name, u.Username, u.Email, u.AccessType, u.Active, u.City, u.Birthdate,
        u.PreferredLanguage, u.Gender, u.Units, u.Height, u.MaxHeartRate, u.FirstDayOfWeek, u.Timezone);
}

public record ProfileUpdate(
    [Required, MaxLength(250)] string Name,
    [Required, EmailAddress, MaxLength(250)] string Email,
    [MaxLength(250)] string? City,
    DateOnly? Birthdate,
    [Required, MaxLength(20)] string PreferredLanguage,
    [Required, AllowedValues("male", "female", "unspecified")] string Gender,
    [Required, AllowedValues("metric", "imperial")] string Units,
    [Range(50, 300)] int? Height,
    [Range(60, 250)] int? MaxHeartRate,
    [Required, AllowedValues("sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday")] string FirstDayOfWeek,
    [Required, MaxLength(64)] string Timezone);

public record AdminUserWrite(
    [Required, MaxLength(250)] string Name,
    [Required, RegularExpression(@"^[a-zA-Z0-9.]{1,45}$")] string Username,
    [Required, EmailAddress, MaxLength(250)] string Email,
    [Required, AllowedValues(AccessTypes.Regular, AccessTypes.Admin)] string AccessType,
    bool Active,
    string? Password);

public record PasswordChange([Required] string CurrentPassword, [Required] string NewPassword);

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/users/me").RequireAuthorization();

        me.MapGet("", async (HttpContext http, AppDb db) => UserRead.From((await db.Users.FindAsync(http.User.UserId()))!));

        me.MapPut("", async (ProfileUpdate req, HttpContext http, AppDb db) =>
        {
            var user = (await db.Users.FindAsync(http.User.UserId()))!;
            if (TimeZoneInfo.TryFindSystemTimeZoneById(req.Timezone, out _) is false) return Results.Problem("Unknown timezone", statusCode: 400);
            if (await db.Users.AnyAsync(u => u.Email == req.Email.ToLower() && u.Id != user.Id)) return Results.Problem("Email already in use", statusCode: 409);
            (user.Name, user.Email, user.City, user.Birthdate, user.PreferredLanguage, user.Gender, user.Units, user.Height, user.MaxHeartRate, user.FirstDayOfWeek, user.Timezone) =
                (req.Name, req.Email.ToLower(), req.City, req.Birthdate, req.PreferredLanguage, req.Gender, req.Units, req.Height, req.MaxHeartRate, req.FirstDayOfWeek, req.Timezone);
            await db.SaveChangesAsync();
            return Results.Ok(UserRead.From(user));
        });

        me.MapPut("/password", async (PasswordChange req, HttpContext http, AppDb db, IPasswordHasher<User> hasher) =>
        {
            var user = (await db.Users.FindAsync(http.User.UserId()))!;
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
                return Results.Problem("Current password is incorrect", statusCode: 400);
            if (PasswordPolicy.Validate(req.NewPassword, user.AccessType) is { } error) return Results.Problem(error, statusCode: 400);
            user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
            // Sign out every other device.
            var current = http.User.FindFirst(TokenService.SessionClaim)!.Value;
            await db.Sessions.Where(s => s.UserId == user.Id && s.Id != current).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var admin = app.MapGroup("/api/v1/users").RequireAuthorization(p => p.RequireRole(AccessTypes.Admin));

        admin.MapGet("", async (AppDb db) => (await db.Users.OrderBy(u => u.Username).ToListAsync()).Select(UserRead.From));

        admin.MapPost("", async (AdminUserWrite req, AppDb db, IPasswordHasher<User> hasher) =>
        {
            if ((req.Password is null ? "Password is required" : PasswordPolicy.Validate(req.Password, req.AccessType)) is { } error)
                return Results.Problem(error, statusCode: 400);
            if (await db.Users.AnyAsync(u => u.Username == req.Username.ToLower() || u.Email == req.Email.ToLower()))
                return Results.Problem("Username or email already in use", statusCode: 409);
            var user = new User { Name = req.Name, Username = req.Username.ToLower(), Email = req.Email.ToLower(), AccessType = req.AccessType, Active = req.Active, PasswordHash = "" };
            user.PasswordHash = hasher.HashPassword(user, req.Password!);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/users/{user.Id}", UserRead.From(user));
        });

        admin.MapPut("/{id:int}", async (int id, AdminUserWrite req, HttpContext http, AppDb db, IPasswordHasher<User> hasher) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            if (id == http.User.UserId() && (req.AccessType != AccessTypes.Admin || !req.Active))
                return Results.Problem("You can't remove your own admin access or deactivate yourself", statusCode: 400);
            if (await db.Users.AnyAsync(u => (u.Username == req.Username.ToLower() || u.Email == req.Email.ToLower()) && u.Id != id))
                return Results.Problem("Username or email already in use", statusCode: 409);
            bool revokeSessions = req.Password is not null || req.AccessType != user.AccessType || !req.Active;
            if (req.Password is not null)
            {
                if (PasswordPolicy.Validate(req.Password, req.AccessType) is { } error) return Results.Problem(error, statusCode: 400);
                user.PasswordHash = hasher.HashPassword(user, req.Password);
            }
            (user.Name, user.Username, user.Email, user.AccessType, user.Active) = (req.Name, req.Username.ToLower(), req.Email.ToLower(), req.AccessType, req.Active);
            // Role, status or password changes take effect immediately by ending the user's sessions.
            if (revokeSessions) await db.Sessions.Where(s => s.UserId == id).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            return Results.Ok(UserRead.From(user));
        });

        admin.MapDelete("/{id:int}", async (int id, HttpContext http, AppDb db) =>
        {
            if (id == http.User.UserId()) return Results.Problem("You can't delete yourself", statusCode: 400);
            return await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound();
        });
    }
}
