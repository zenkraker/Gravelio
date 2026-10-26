using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Auth;

public sealed class ResetPasswordRequest
{
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}

public sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(128);
    }
}

public sealed class ResetPasswordEndpoint(AppDb db, UserManager<User> users, TimeProvider time) : Endpoint<ResetPasswordRequest>
{
    const string InvalidLink = "This password reset link is invalid or has expired.";

    public override void Configure()
    {
        Post("/auth/reset-password");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting("sensitive"));
    }

    public override async Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var hash = PasswordReset.Hash(req.Token);
        var token = await db.PasswordResetTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.ExpiresAt <= time.GetUtcNow().UtcDateTime) ThrowError(InvalidLink, StatusCodes.Status400BadRequest);

        var user = await users.FindByIdAsync(token.UserId.ToString());
        if (user is not { Active: true }) ThrowError(InvalidLink, StatusCodes.Status400BadRequest);

        // Identity's own reset token carries the password-policy validation; ours only proves the link is genuine.
        var identityToken = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, identityToken, req.NewPassword);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));

        // Consumed: the link cannot be replayed, and older links for the same account stop working too.
        await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
