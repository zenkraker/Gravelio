using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Sso;

/// Disconnects the account from a provider, unless that would leave no way to sign in.
public sealed class UnlinkSsoEndpoint(UserManager<User> users, SignInManager<User> signIn) : Endpoint<UnlinkSsoRequest>
{
    public override void Configure() => Delete("/users/me/sso/{slug}");

    public override async Task HandleAsync(UnlinkSsoRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        var logins = await users.GetLoginsAsync(user);
        var login = logins.FirstOrDefault(l => l.LoginProvider == req.Slug);
        if (login is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (!await users.HasPasswordAsync(user) && logins.Count == 1)
            ThrowError("This is the only way you can sign in. Set a password first (use \"forgot password\"), then unlink.", StatusCodes.Status409Conflict);
        var result = await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        await signIn.RefreshSignInAsync(user); // removing a login rotates the security stamp, which would end this very session
        await Send.NoContentAsync(ct);
    }
}
