using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class ChangePasswordEndpoint(UserManager<User> users, SignInManager<User> signIn) : Endpoint<ChangePasswordRequest>
{
    public override void Configure() => Put("/users/me/password");

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        // ChangePasswordAsync rotated the security stamp, which signs out every other session; keep this one.
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}
