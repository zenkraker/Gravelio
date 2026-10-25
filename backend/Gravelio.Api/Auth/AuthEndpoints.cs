using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginEndpoint(SignInManager<User> signIn, UserManager<User> users) : Endpoint<LoginRequest>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting("sensitive"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(req.Username);
        if (user is null)
        {
            // Unknown users still pay for a hash so response time doesn't reveal which usernames exist.
            users.PasswordHasher.HashPassword(new User { Name = "", UserName = "" }, req.Password);
            AddError("Incorrect username or password");
            await Send.ErrorsAsync(StatusCodes.Status401Unauthorized, ct);
            return;
        }
        if (!user.Active)
        {
            AddError("Inactive user");
            await Send.ErrorsAsync(StatusCodes.Status403Forbidden, ct);
            return;
        }

        var result = await signIn.PasswordSignInAsync(user, req.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            AddError("Account locked due to too many failed login attempts. Try again later.");
            await Send.ErrorsAsync(StatusCodes.Status429TooManyRequests, ct);
        }
        else if (!result.Succeeded)
        {
            AddError("Incorrect username or password");
            await Send.ErrorsAsync(StatusCodes.Status401Unauthorized, ct);
        }
        else
        {
            await Send.NoContentAsync(ct);
        }
    }
}

public sealed class LogoutEndpoint(SignInManager<User> signIn) : EndpointWithoutRequest
{
    public override void Configure() => Post("/auth/logout");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await signIn.SignOutAsync();
        await Send.NoContentAsync(ct);
    }
}
