using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class UpdateMeEndpoint(UserManager<User> users, SignInManager<User> signIn) : Endpoint<UpdateMeRequest, UserRead>
{
    public override void Configure() => Put("/users/me");

    public override async Task HandleAsync(UpdateMeRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        (user.Name, user.City, user.Birthdate, user.PreferredLanguage, user.Gender, user.Units, user.Height, user.MaxHeartRate, user.FirstDayOfWeek, user.Timezone, user.Currency) =
            (req.Name, req.City, req.Birthdate, req.PreferredLanguage, req.Gender, req.Units, req.Height, req.MaxHeartRate, req.FirstDayOfWeek, req.Timezone, req.Currency);
        // SetEmailAsync rotates the security stamp, which signs every session out, so only call it when the email changed.
        bool emailChanged = !string.Equals(user.Email, req.Email, StringComparison.OrdinalIgnoreCase);
        var result = emailChanged ? await users.SetEmailAsync(user, req.Email) : IdentityResult.Success;
        if (result.Succeeded) result = await users.UpdateAsync(user);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        if (emailChanged) await signIn.RefreshSignInAsync(user); // keep this session; other devices sign in again
        await Send.OkAsync(UserRead.From(user, await users.IsInRoleAsync(user, AppRoles.Admin)), ct);
    }
}
