using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sso;

public sealed class ListSsoProvidersRequest : PagedRequest;

public sealed class ListSsoProvidersValidator : PagedValidator<ListSsoProvidersRequest>;

public sealed class ListSsoProvidersEndpoint(AppDb db) : Endpoint<ListSsoProvidersRequest, PagedResult<SsoProviderRead>>
{
    public override void Configure()
    {
        Get("");
        Group<SsoAdminGroup>();
    }

    public override async Task HandleAsync(ListSsoProvidersRequest req, CancellationToken ct) =>
        await Send.OkAsync(await db.IdentityProviders.AsNoTracking().OrderBy(p => p.Name).ThenBy(p => p.Id)
            .ToPageAsync(req, page => page.Select(SsoProviderRead.Projection), ct), ct);
}
