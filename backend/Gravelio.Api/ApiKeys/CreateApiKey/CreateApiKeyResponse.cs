namespace Gravelio.Api.ApiKeys;

/// The only time the key itself is shown: the server keeps just its hash.
public sealed record CreateApiKeyResponse(int Id, string Name, string Key, string Prefix, DateTime CreatedAt, DateTime? ExpiresAt);
