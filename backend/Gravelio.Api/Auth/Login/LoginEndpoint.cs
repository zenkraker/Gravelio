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
        var result = await signIn.PasswordSignInAsync(user, req.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut) ThrowError("Account locked due to too many failed login attempts. Try again later.", StatusCodes.Status429TooManyRequests);
        if (!result.Succeeded) ThrowError("Incorrect username or password", StatusCodes.Status401Unauthorized);
        await Send.NoContentAsync(ct);
    }
}
