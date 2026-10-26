using Gravelio.Api.Common;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Gravelio.Api.Settings;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed class SignupRequest
{
    public required string Name { get; init; }
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed class SignupValidator : Validator<SignupRequest>
{
    public SignupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Username).Matches(UserRules.UsernamePattern)
            .WithMessage("Username may only contain letters, digits and dots (max 45).");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(250);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

/// What the new account still needs before its owner can sign in.
public sealed record SignupRead(bool PendingEmailVerification, bool PendingApproval);

/// Self sign-up, allowed only while an admin has enabled it. Email verification and admin approval follow the server
/// settings saved at the time of sign-up.
public sealed class SignupEndpoint(AppDb db, UserManager<User> users, IEmailSender email, TimeProvider time,
    ILogger<SignupEndpoint> log) : Endpoint<SignupRequest, SignupRead>
{
    public override void Configure()
    {
        Post("/auth/signup");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(SignupRequest req, CancellationToken ct)
    {
        var settings = await ServerSettingsStore.GetAsync(db, ct);
        if (!settings.SignupEnabled) ThrowError("Sign-up is disabled on this server.", StatusCodes.Status403Forbidden);

        var user = new User
        {
            Name = req.Name,
            UserName = req.Username,
            Email = req.Email,
            Active = !settings.SignupRequireAdminApproval,
            PendingApproval = settings.SignupRequireAdminApproval,
            EmailConfirmed = !settings.SignupRequireEmailVerification,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));

        if (!user.EmailConfirmed)
        {
            // Identity signs the confirmation token; the link has to carry the user id because the token is not a lookup key.
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/verify-email" +
                       $"?user={user.Id}&token={Uri.EscapeDataString(token)}";
            try
            {
                await email.SendAsync(user.Email!, "Confirm your Gravelio account",
                    $"""
                     Welcome to Gravelio.

                     Confirm your email address to finish creating your account:
                     {link}

                     If you did not sign up, ignore this email.
                     """, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "Could not send an account confirmation email");
            }
        }

        if (user.PendingApproval && user.EmailConfirmed) await SignupApprovals.NotifyAdminsAsync(db, users, user, time.GetUtcNow().UtcDateTime, log, ct);

        await Send.OkAsync(new SignupRead(!user.EmailConfirmed, !user.Active), ct);
    }
}
