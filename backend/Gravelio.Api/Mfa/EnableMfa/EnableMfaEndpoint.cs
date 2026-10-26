using Gravelio.Api.Common;
using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Mfa;

public sealed class EnableMfaEndpoint(UserManager<User> users, SignInManager<User> signIn, AppDb db, TimeProvider time) : Endpoint<EnableMfaRequest, EnableMfaResponse>
{
    public override void Configure()
    {
        Post("/users/me/mfa/enable");
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(EnableMfaRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (user.TwoFactorEnabled) ThrowError("MFA is already on.", StatusCodes.Status409Conflict);
        var check = await MfaCodes.StepUpAsync(users, db, time, user, req.CurrentPassword, req.Code, requireCode: true, ct);
        if (check == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
        if (check != MfaResult.Ok) ThrowError(MfaCodes.InvalidMessage, StatusCodes.Status400BadRequest);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await users.UpdateSecurityStampAsync(user); // other devices sign in again, now with MFA
        await transaction.CommitAsync(ct);
        await signIn.RefreshSignInAsync(user);
        await Send.OkAsync(new EnableMfaResponse(codes!.ToArray()), ct);
    }
}
