using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Settings;

/// Sends one message to the admin's own address, so a person can check the mail setup before relying on it for resets and sign-ups.
public sealed class SendTestEmailEndpoint(UserManager<User> users, IEmailSender email, ILogger<SendTestEmailEndpoint> log) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/email/test");
        Group<AdminSettingsGroup>();
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!email.IsConfigured) ThrowError("Email is not configured on this server.", StatusCodes.Status409Conflict);
        var me = await users.GetUserAsync(User);
        try
        {
            await email.SendAsync(me!.Email!, "Gravelio test email", "This is a test message from your Gravelio server. Email is working.", ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "The test email could not be sent");
            ThrowError("The server could not send the email. Check the mail settings and the server log.", StatusCodes.Status502BadGateway);
        }
        await Send.NoContentAsync(ct);
    }
}
