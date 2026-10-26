using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class UpdateUserEndpoint(UserManager<User> users, AppDb db) : Endpoint<UpdateUserRequest, UserRead>
{
    public override void Configure()
    {
        Put("{id:int}");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(req.Id.ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        bool wasAdmin = await users.IsInRoleAsync(user, AppRoles.Admin);
        bool makeAdmin = req.AccessType == AppRoles.Admin;
        if (req.Id == User.UserId() && (!makeAdmin || !req.Active))
            ThrowError("You can't remove your own admin access or deactivate yourself", StatusCodes.Status400BadRequest);

        // One transaction: a rejected password or a duplicate name must leave the account exactly as it was.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        user.Name = req.Name;
        user.Active = req.Active;
        if (req.Active) user.PendingApproval = false; // an admin who activates the account has approved it
        var result = await users.SetUserNameAsync(user, req.Username);
        if (result.Succeeded) result = await users.SetEmailAsync(user, req.Email);
        // SetEmailAsync clears confirmation even for an unchanged address; an admin sets the address, so it is confirmed.
        if (result.Succeeded) user.EmailConfirmed = true;
        if (result.Succeeded && makeAdmin != wasAdmin)
            result = makeAdmin ? await users.AddToRoleAsync(user, AppRoles.Admin) : await users.RemoveFromRoleAsync(user, AppRoles.Admin);
        if (result.Succeeded && req.Password is not null)
            result = await users.RemovePasswordAsync(user) is { Succeeded: true } ? await users.AddPasswordAsync(user, req.Password) : IdentityResult.Failed();
        if (result.Succeeded) result = await users.UpdateAsync(user);
        // Role, status and password changes must end the user's current sessions.
        if (result.Succeeded && (makeAdmin != wasAdmin || !req.Active || req.Password is not null)) result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        await transaction.CommitAsync(ct);
        await Send.OkAsync(UserRead.From(user, makeAdmin), ct);
    }
}
