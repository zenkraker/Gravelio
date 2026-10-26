using Gravelio.Api.Data;

namespace Gravelio.Api.ApiKeys;

public sealed record ApiKeyRead(int Id, string Name, string Prefix, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt, DateTime? RevokedAt)
{
    public static ApiKeyRead From(ApiKey k) => new(k.Id, k.Name, k.Prefix, k.CreatedAt, k.ExpiresAt, k.LastUsedAt, k.RevokedAt);
}
