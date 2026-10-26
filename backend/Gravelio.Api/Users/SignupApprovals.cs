using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Notifications;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public static class SignupApprovals
{
    /// Tells every active admin that a new account waits for approval. Called once the sign-up is complete, which is after the
    /// email is confirmed when the server asks for that, so admins never hear about an address nobody owns.
    public static async Task NotifyAdminsAsync(AppDb db, UserManager<User> users, User newUser, DateTime now, ILogger log, CancellationToken ct)
    {
        foreach (var admin in await users.GetUsersInRoleAsync(AppRoles.Admin))
            if (admin.Active) NotificationWriter.Add(db, admin.Id, NotificationTypes.AdminSignUpApprovalRequest, newUser.Id, newUser.Name, now);
        await NotificationWriter.TrySaveAsync(db, log, ct);
    }
}
