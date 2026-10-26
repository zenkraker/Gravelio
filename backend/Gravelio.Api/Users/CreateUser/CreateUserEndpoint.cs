using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class CreateUserEndpoint(UserManager<User> users, TimeProvider time) : Endpoint<CreateUserRequest, UserRead>
{
    public override void Configure()
    {
        Post("");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (req.Password is null) ThrowError(r => r.Password, "Password is required", StatusCodes.Status400BadRequest);
        var user = new User { Name = req.Name, UserName = req.Username, Email = req.Email, Active = req.Active, EmailConfirmed = true, CreatedAt = time.GetUtcNow().UtcDateTime };
        var result = await users.CreateAsync(user, req.Password);
        if (result.Succeeded && req.AccessType == AppRoles.Admin) result = await users.AddToRoleAsync(user, AppRoles.Admin);
        if (!result.Succeeded) ThrowError(UserRules.Describe(result), UserRules.StatusOf(result));
        await Send.CreatedAtAsync<ListUsersEndpoint>(null, UserRead.From(user, req.AccessType == AppRoles.Admin), cancellation: ct);
    }
}
