using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

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
        if (user is not { Active: true })
        {
            // Unknown and deactivated users get the same 401 and still pay for a hash, so neither the response
            // nor its timing reveals which accounts exist.
            users.PasswordHasher.HashPassword(new User { Name = "", UserName = "" }, req.Password);
            ThrowError("Incorrect username or password", StatusCodes.Status401Unauthorized);
        }
        // The password is checked before the cookie is issued, so an unconfirmed account never gets a session.
        var result = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (result.IsLockedOut) ThrowError("Account locked due to too many failed login attempts. Try again later.", StatusCodes.Status429TooManyRequests);
        if (!result.Succeeded) ThrowError("Incorrect username or password", StatusCodes.Status401Unauthorized);
        // Only revealed to someone who proved the password.
        if (!user.EmailConfirmed) ThrowError("Confirm your email address before signing in.", StatusCodes.Status401Unauthorized);
        await signIn.SignInAsync(user, isPersistent: true);
        await Send.NoContentAsync(ct);
    }
}
