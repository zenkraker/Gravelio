using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Mfa;

public sealed class RegenerateRecoveryCodesEndpoint(UserManager<User> users, AppDb db, TimeProvider time)
    : Endpoint<RegenerateRecoveryCodesRequest, RegenerateRecoveryCodesResponse>
{
    public override void Configure()
    {
        Post("/users/me/mfa/recovery-codes");
        Options(o => o.RequireRateLimiting("sensitive"));
    }

    public override async Task HandleAsync(RegenerateRecoveryCodesRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!user.TwoFactorEnabled) ThrowError("MFA is not on.", StatusCodes.Status409Conflict);
        var check = await MfaCodes.StepUpAsync(users, db, time, user, req.CurrentPassword, req.Code, requireCode: true, ct);
        if (check == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
        if (check != MfaResult.Ok) ThrowError(MfaCodes.InvalidMessage, StatusCodes.Status400BadRequest);

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await Send.OkAsync(new RegenerateRecoveryCodesResponse(codes!.ToArray()), ct);
    }
}
