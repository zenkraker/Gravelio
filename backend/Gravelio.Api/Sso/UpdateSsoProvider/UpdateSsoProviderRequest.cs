namespace Gravelio.Api.Sso;

public sealed class UpdateSsoProviderRequest
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Issuer { get; init; }
    public required string ClientId { get; init; }

    /// Null keeps the stored secret; a value replaces it.
    public string? ClientSecret { get; init; }
    public bool ClearClientSecret { get; init; }
    public string? Scopes { get; init; }
    public bool Enabled { get; init; } = true;
    public bool AutoCreateUsers { get; init; }
}
