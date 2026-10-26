using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Mfa;

public sealed class DisableMfaEndpoint(UserManager<User> users, SignInManager<User> signIn, AppDb db, TimeProvider time) : Endpoint<DisableMfaRequest>
{
    public override void Configure()
    {
        Post("/users/me/mfa/disable");
        Options(o => o.RequireRateLimiting("sensitive"));
    }

    public override async Task HandleAsync(DisableMfaRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!user.TwoFactorEnabled) ThrowError("MFA is not on.", StatusCodes.Status409Conflict);
        var check = await MfaCodes.StepUpAsync(users, db, time, user, req.CurrentPassword, req.Code, requireCode: true, ct);
        if (check == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
        if (check != MfaResult.Ok) ThrowError(MfaCodes.InvalidMessage, StatusCodes.Status400BadRequest);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user); // the old key must not work if MFA is switched on again
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0); // and neither must the old recovery codes
        await users.UpdateSecurityStampAsync(user);
        await transaction.CommitAsync(ct);
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}
