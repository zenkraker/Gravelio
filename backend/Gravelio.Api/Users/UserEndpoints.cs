using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed record UserRead(int Id, string Name, string Username, string Email, string AccessType, bool Active, string? City,
    DateOnly? Birthdate, string PreferredLanguage, string Gender, string Units, int? Height, int? MaxHeartRate,
    string FirstDayOfWeek, string Timezone, string Currency)
{
    public static UserRead From(User u, bool isAdmin) => new(u.Id, u.Name, u.UserName!, u.Email!, isAdmin ? AppRoles.Admin : "regular", u.Active,
        u.City, u.Birthdate, u.PreferredLanguage, u.Gender, u.Units, u.Height, u.MaxHeartRate, u.FirstDayOfWeek, u.Timezone, u.Currency);
}

static class UserRules
{
    public static readonly string[] Genders = ["male", "female", "unspecified"];
    public static readonly string[] Units = ["metric", "imperial"];
    public static readonly string[] AccessTypes = ["regular", AppRoles.Admin];
    public static readonly string[] Currencies = ["euro", "dollar", "pound"];
    public static readonly string[] Days = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    // Letters, digits and dots, as in Endurain.
    public const string UsernamePattern = "^[a-zA-Z0-9.]{1,45}$";

    public static string Describe(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Description));
}

public sealed class GetMeEndpoint(UserManager<User> users) : EndpointWithoutRequest<UserRead>
{
    public override void Configure() => Get("/users/me");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        await Send.OkAsync(UserRead.From(user, await users.IsInRoleAsync(user, AppRoles.Admin)), ct);
    }
}

public sealed record UpdateMeRequest(string Name, string Email, string? City, DateOnly? Birthdate, string PreferredLanguage,
    string Gender, string Units, int? Height, int? MaxHeartRate, string FirstDayOfWeek, string Timezone, string Currency = "euro");

public sealed class UpdateMeValidator : Validator<UpdateMeRequest>
{
    public UpdateMeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(250);
        RuleFor(x => x.City).MaximumLength(250);
        RuleFor(x => x.PreferredLanguage).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Gender).Must(UserRules.Genders.Contains).WithMessage("Unknown gender");
        RuleFor(x => x.Units).Must(UserRules.Units.Contains).WithMessage("Unknown units");
        RuleFor(x => x.Height).InclusiveBetween(50, 300);
        RuleFor(x => x.MaxHeartRate).InclusiveBetween(60, 250);
        RuleFor(x => x.FirstDayOfWeek).Must(UserRules.Days.Contains).WithMessage("Unknown day of week");
        RuleFor(x => x.Currency).Must(UserRules.Currencies.Contains).WithMessage("Unknown currency");
        RuleFor(x => x.Timezone).Must(tz => TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _)).WithMessage("Unknown timezone");
    }
}

public sealed class UpdateMeEndpoint(UserManager<User> users, SignInManager<User> signIn) : Endpoint<UpdateMeRequest, UserRead>
{
    public override void Configure() => Put("/users/me");

    public override async Task HandleAsync(UpdateMeRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        (user.Name, user.City, user.Birthdate, user.PreferredLanguage, user.Gender, user.Units, user.Height, user.MaxHeartRate, user.FirstDayOfWeek, user.Timezone, user.Currency) =
            (req.Name, req.City, req.Birthdate, req.PreferredLanguage, req.Gender, req.Units, req.Height, req.MaxHeartRate, req.FirstDayOfWeek, req.Timezone, req.Currency);
        // SetEmailAsync rotates the security stamp, which signs every session out, so only call it when the email changed.
        bool emailChanged = !string.Equals(user.Email, req.Email, StringComparison.OrdinalIgnoreCase);
        var result = emailChanged ? await users.SetEmailAsync(user, req.Email) : IdentityResult.Success;
        if (result.Succeeded) result = await users.UpdateAsync(user);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), StatusCodes.Status409Conflict);
        if (emailChanged) await signIn.RefreshSignInAsync(user); // keep this session; other devices sign in again
        await Send.OkAsync(UserRead.From(user, await users.IsInRoleAsync(user, AppRoles.Admin)), ct);
    }
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(128);
    }
}

public sealed class ChangePasswordEndpoint(UserManager<User> users, SignInManager<User> signIn) : Endpoint<ChangePasswordRequest>
{
    public override void Configure() => Put("/users/me/password");

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            ThrowError(UserRules.Describe(result), StatusCodes.Status400BadRequest);
        }
        // ChangePasswordAsync rotated the security stamp, which signs out every other session; keep this one.
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}

public sealed record AdminUserRequest(int Id, string Name, string Username, string Email, string AccessType, bool Active, string? Password);

public sealed class AdminUserValidator : Validator<AdminUserRequest>
{
    public AdminUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Username).Matches(UserRules.UsernamePattern).WithMessage("Username may only contain letters, digits and dots (max 45).");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(250);
        RuleFor(x => x.AccessType).Must(UserRules.AccessTypes.Contains).WithMessage("Unknown access type");
        RuleFor(x => x.Password).MaximumLength(128);
        RuleFor(x => x.Password).Must(p => p is null or { Length: >= AdminPasswordValidator.AdminMinLength })
            .When(x => x.AccessType == AppRoles.Admin).WithMessage(AdminPasswordValidator.TooShortMessage);
    }
}

public sealed class ListUsersEndpoint(AppDb db) : EndpointWithoutRequest<List<UserRead>>
{
    public override void Configure()
    {
        Get("/users");
        Roles(AppRoles.Admin);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var adminIds = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where r.Name == AppRoles.Admin select ur.UserId)
            .ToListAsync(ct);
        var all = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        await Send.OkAsync(all.Select(u => UserRead.From(u, adminIds.Contains(u.Id))).ToList(), ct);
    }
}

public sealed class CreateUserEndpoint(UserManager<User> users, TimeProvider time) : Endpoint<AdminUserRequest, UserRead>
{
    public override void Configure()
    {
        Post("/users");
        Roles(AppRoles.Admin);
    }

    public override async Task HandleAsync(AdminUserRequest req, CancellationToken ct)
    {
        if (req.Password is null)
        {
            ThrowError(r => r.Password, "Password is required", StatusCodes.Status400BadRequest);
        }
        var user = new User { Name = req.Name, UserName = req.Username, Email = req.Email, Active = req.Active, CreatedAt = time.GetUtcNow().UtcDateTime };
        var result = await users.CreateAsync(user, req.Password);
        if (result.Succeeded && req.AccessType == AppRoles.Admin) result = await users.AddToRoleAsync(user, AppRoles.Admin);
        if (!result.Succeeded)
        {
            ThrowError(UserRules.Describe(result), result.Errors.Any(e => e.Code.StartsWith("Duplicate")) ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
        }
        await Send.CreatedAtAsync<ListUsersEndpoint>(null, UserRead.From(user, req.AccessType == AppRoles.Admin), cancellation: ct);
    }
}

public sealed class UpdateUserEndpoint(UserManager<User> users, AppDb db) : Endpoint<AdminUserRequest, UserRead>
{
    public override void Configure()
    {
        Put("/users/{id:int}");
        Roles(AppRoles.Admin);
    }

    public override async Task HandleAsync(AdminUserRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(req.Id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        bool wasAdmin = await users.IsInRoleAsync(user, AppRoles.Admin);
        bool makeAdmin = req.AccessType == AppRoles.Admin;
        if (req.Id == User.UserId() && (!makeAdmin || !req.Active))
        {
            ThrowError("You can't remove your own admin access or deactivate yourself", StatusCodes.Status400BadRequest);
        }

        // One transaction: a rejected password or a duplicate name must leave the account exactly as it was.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        user.Name = req.Name;
        user.Active = req.Active;
        var result = await users.SetUserNameAsync(user, req.Username);
        if (result.Succeeded) result = await users.SetEmailAsync(user, req.Email);
        if (result.Succeeded && makeAdmin != wasAdmin)
            result = makeAdmin ? await users.AddToRoleAsync(user, AppRoles.Admin) : await users.RemoveFromRoleAsync(user, AppRoles.Admin);
        if (result.Succeeded && req.Password is not null)
            result = await users.RemovePasswordAsync(user) is { Succeeded: true } ? await users.AddPasswordAsync(user, req.Password) : IdentityResult.Failed();
        if (result.Succeeded) result = await users.UpdateAsync(user);
        // Role, status and password changes must end the user's current sessions.
        if (result.Succeeded && (makeAdmin != wasAdmin || !req.Active || req.Password is not null)) result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            ThrowError(UserRules.Describe(result), StatusCodes.Status400BadRequest);
        }
        await transaction.CommitAsync(ct);
        await Send.OkAsync(UserRead.From(user, makeAdmin), ct);
    }
}

public sealed record DeleteUserRequest(int Id);

public sealed class DeleteUserEndpoint(UserManager<User> users) : Endpoint<DeleteUserRequest>
{
    public override void Configure()
    {
        Delete("/users/{id:int}");
        Roles(AppRoles.Admin);
    }

    public override async Task HandleAsync(DeleteUserRequest req, CancellationToken ct)
    {
        if (req.Id == User.UserId())
        {
            ThrowError("You can't delete yourself", StatusCodes.Status400BadRequest);
        }
        var user = await users.FindByIdAsync(req.Id.ToString());
        if (user is null) await Send.NotFoundAsync(ct);
        else
        {
            await users.DeleteAsync(user);
            await Send.NoContentAsync(ct);
        }
    }
}
