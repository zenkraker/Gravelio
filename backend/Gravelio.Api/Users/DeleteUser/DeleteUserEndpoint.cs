using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Users;

public sealed class DeleteUserEndpoint(UserManager<User> users) : Endpoint<IdRequest>
{
    public override void Configure()
    {
        Delete("{id:int}");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (req.Id == User.UserId()) ThrowError("You can't delete yourself", StatusCodes.Status400BadRequest);
        var user = await users.FindByIdAsync(req.Id.ToString());
        if (user is null) await Send.NotFoundAsync(ct);
        else
        {
            await users.DeleteAsync(user);
            await Send.NoContentAsync(ct);
        }
    }
}
