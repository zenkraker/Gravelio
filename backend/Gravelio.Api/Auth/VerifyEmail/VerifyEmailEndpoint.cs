using Gravelio.Api.Common;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed class VerifyEmailRequest
{
    public int UserId { get; init; }
    public required string Token { get; init; }
}

public sealed class VerifyEmailValidator : Validator<VerifyEmailRequest>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class VerifyEmailEndpoint(UserManager<User> users) : Endpoint<VerifyEmailRequest>
{
    const string InvalidLink = "This confirmation link is invalid or has expired.";

    public override void Configure()
    {
        Post("/auth/verify-email");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(req.UserId.ToString());
        // Confirming twice is not an error the person caused, but the token itself is single-use.
        if (user is null) ThrowError(InvalidLink, StatusCodes.Status400BadRequest);
        if (user.EmailConfirmed) ThrowError(InvalidLink, StatusCodes.Status400BadRequest);

        var result = await users.ConfirmEmailAsync(user, req.Token);
        if (!result.Succeeded) ThrowError(InvalidLink, StatusCodes.Status400BadRequest);
        await Send.NoContentAsync(ct);
    }
}
