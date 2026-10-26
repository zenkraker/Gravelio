using FastEndpoints;
using Gravelio.Api.Auth;

namespace Gravelio.Api.Sso;

/// Everything under /sso-providers that only admins may call.
public sealed class SsoAdminGroup : Group
{
    public SsoAdminGroup() => Configure("sso-providers", ep => ep.Roles(AppRoles.Admin));
}
