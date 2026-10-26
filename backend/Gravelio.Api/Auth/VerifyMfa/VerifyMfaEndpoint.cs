using Gravelio.Api.Common;
using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Mfa;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

/// Second step of signing in to an account with MFA. The password was already accepted by <see cref="LoginEndpoint"/>, which left Identity's
/// short-lived two-factor cookie; without it there is nobody to verify.
public sealed class VerifyMfaEndpoint(SignInManager<User> signIn, UserManager<User> users, AppDb db, TimeProvider time) : Endpoint<VerifyMfaRequest>
{
    public override void Configure()
    {
        Post("/auth/mfa/verify");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(VerifyMfaRequest req, CancellationToken ct)
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is not { Active: true }) ThrowError("Your sign-in expired. Enter your password again.", StatusCodes.Status401Unauthorized);
        var result = await MfaCodes.VerifyAsync(users, db, time, user, req.Code, ct);
        if (result == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
        if (result != MfaResult.Ok) ThrowError("Incorrect code", StatusCodes.Status401Unauthorized);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        await signIn.SignInAsync(user, isPersistent: true);
        await Send.NoContentAsync(ct);
    }
}
