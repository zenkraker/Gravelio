using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sso;

public sealed record MySsoRead(string Slug, string Name, bool Linked);

// Unpaged on purpose: one row per provider an admin has set up, which is a handful.
public sealed class ListMySsoEndpoint(AppDb db, UserManager<User> users) : EndpointWithoutRequest<List<MySsoRead>>
{
    public override void Configure() => Get("/users/me/sso");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        var linked = (await users.GetLoginsAsync(user)).Select(l => l.LoginProvider).ToHashSet();
        var providers = await db.IdentityProviders.AsNoTracking().Where(p => p.Enabled).OrderBy(p => p.Name).Select(p => new { p.Slug, p.Name }).ToListAsync(ct);
        await Send.OkAsync([.. providers.Select(p => new MySsoRead(p.Slug, p.Name, linked.Contains(p.Slug)))], ct);
    }
}
