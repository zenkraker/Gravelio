using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class GetMeEndpoint(UserManager<User> users) : EndpointWithoutRequest<UserRead>
{
    public override void Configure() => Get("/users/me");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        await Send.OkAsync(UserRead.From(user, await users.IsInRoleAsync(user, AppRoles.Admin)), ct);
    }
}
