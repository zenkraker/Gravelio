using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Mfa;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class ChangePasswordEndpoint(UserManager<User> users, SignInManager<User> signIn, AppDb db, TimeProvider time) : Endpoint<ChangePasswordRequest>
{
    public override void Configure() => Put("/users/me/password");

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (user.TwoFactorEnabled)
        {
            var check = string.IsNullOrWhiteSpace(req.Code) ? MfaResult.Invalid : await MfaCodes.VerifyAsync(users, db, time, user, req.Code, ct);
            if (check == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
            if (check != MfaResult.Ok) ThrowError("Enter a valid authenticator or recovery code.", StatusCodes.Status400BadRequest);
        }
        var result = await users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        // ChangePasswordAsync rotated the security stamp, which signs out every other session; keep this one.
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}
