using Gravelio.Api.Common;
using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Auth;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}

/// Starts a password reset. The answer is always the same, so the endpoint never reveals whether an account exists.
public sealed class ForgotPasswordEndpoint(AppDb db, UserManager<User> users, IEmailSender email, TimeProvider time,
    ILogger<ForgotPasswordEndpoint> log) : Endpoint<ForgotPasswordRequest>
{
    public override void Configure()
    {
        Post("/auth/forgot-password");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(req.Email.Trim());
        if (user is { Active: true, Email: { } address })
        {
            var raw = PasswordReset.NewToken();
            var now = time.GetUtcNow().UtcDateTime;
            await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = PasswordReset.Hash(raw),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(PasswordReset.LifetimeMinutes),
            });
            await db.SaveChangesAsync(ct);

            // The SPA is served from the same origin as the API, and forwarded headers make this the public origin.
            var link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/reset-password?token={raw}";
            try
            {
                await email.SendAsync(address, "Reset your Gravelio password",
                    $"""
                     Someone asked to reset the password for your Gravelio account.

                     Open this link within {PasswordReset.LifetimeMinutes} minutes to choose a new password:
                     {link}

                     If it wasn't you, ignore this email; your password stays unchanged.
                     """, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "Could not send a password reset email");
            }
        }
        await Send.NoContentAsync(ct);
    }
}
