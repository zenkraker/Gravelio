using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.ApiKeys;

// Unpaged on purpose: a user can hold at most ApiKeyAuth.MaxKeysPerUser keys.
public sealed class ListApiKeysEndpoint(AppDb db) : EndpointWithoutRequest<List<ApiKeyRead>>
{
    public override void Configure() => Get("/users/me/api-keys");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.ApiKeys.AsNoTracking().Where(k => k.UserId == userId).OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyRead(k.Id, k.Name, k.Prefix, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.RevokedAt)).ToListAsync(ct), ct);
    }
}
