using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed class ListUsersEndpoint(AppDb db) : Endpoint<ListUsersRequest, PagedResult<UserRead>>
{
    public override void Configure()
    {
        Get("");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var adminRoleId = db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id);
        await Send.OkAsync(await db.Users.AsNoTracking().OrderBy(u => u.UserName).ThenBy(u => u.Id).ToPageAsync(req, page => page.Select(u => new
        {
            User = u,
            IsAdmin = db.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleId.Contains(ur.RoleId)),
        }).Select(x => UserRead.From(x.User, x.IsAdmin)), ct), ct);
    }
}
