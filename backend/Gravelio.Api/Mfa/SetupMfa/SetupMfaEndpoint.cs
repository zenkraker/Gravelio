using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Mfa;

/// Starts MFA setup with a fresh key. Nothing changes for sign-in until the key is confirmed with a code in <see cref="EnableMfaEndpoint"/>.
public sealed class SetupMfaEndpoint(UserManager<User> users, SignInManager<User> signIn) : EndpointWithoutRequest<SetupMfaResponse>
{
    public override void Configure() => Post("/users/me/mfa/setup");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (user.TwoFactorEnabled) ThrowError("MFA is already on. Turn it off before setting it up again.", StatusCodes.Status409Conflict);
        await users.ResetAuthenticatorKeyAsync(user);
        await signIn.RefreshSignInAsync(user); // a new key rotates the security stamp, which would otherwise sign this session out
        var key = (await users.GetAuthenticatorKeyAsync(user))!;
        var account = Uri.EscapeDataString(user.UserName!);
        await Send.OkAsync(new SetupMfaResponse(key, $"otpauth://totp/Gravelio:{account}?secret={key}&issuer=Gravelio&digits=6"), ct);
    }
}
