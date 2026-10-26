namespace Gravelio.Api.ApiKeys;

/// <see cref="ExpiresInDays"/> null means the key never expires. <see cref="Code"/> is required while the account has MFA on.
public sealed record CreateApiKeyRequest(string Name, int? ExpiresInDays, string CurrentPassword, string? Code);
