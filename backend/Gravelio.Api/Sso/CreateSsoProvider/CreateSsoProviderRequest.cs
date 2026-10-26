namespace Gravelio.Api.Sso;

public sealed class CreateSsoProviderRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Issuer { get; init; }
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? Scopes { get; init; }
    public bool Enabled { get; init; } = true;
    public bool AutoCreateUsers { get; init; }
}
