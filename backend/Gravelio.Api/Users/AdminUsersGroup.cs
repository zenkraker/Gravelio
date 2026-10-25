using FastEndpoints;
using Gravelio.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

/// Everything under /users that only admins may call; the role is declared here once.
public sealed class AdminUsersGroup : Group
{
    public AdminUsersGroup() => Configure("users", ep => ep.Roles(AppRoles.Admin));
}
