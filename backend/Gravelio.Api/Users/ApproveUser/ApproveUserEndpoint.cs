using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Email;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

/// Lets a pending sign-up in. A deactivated account is not pending and is reactivated through the user edit instead.
public sealed class ApproveUserEndpoint(UserManager<User> users, IEmailSender email, ILogger<ApproveUserEndpoint> log) : Endpoint<IdRequest, UserRead>
{
    public override void Configure()
    {
        Put("{id:int}/approve");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(req.Id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!user.PendingApproval) ThrowError("This account is not waiting for approval.", StatusCodes.Status409Conflict);

        user.Active = true;
        user.PendingApproval = false;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));

        try
        {
            await email.SendAsync(user.Email!, "Your Gravelio account was approved",
                $"""
                 Hello {user.Name},

                 An administrator approved your account. You can sign in now.
                 """, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Could not send the approval email");
        }
        await Send.OkAsync(UserRead.From(user, isAdmin: false), ct);
    }
}
