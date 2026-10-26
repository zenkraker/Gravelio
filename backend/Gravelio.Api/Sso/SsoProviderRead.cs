using System.Linq.Expressions;
using Gravelio.Api.Data;

namespace Gravelio.Api.Sso;

/// An admin's view of a provider. The client secret is never sent back: only whether one is stored.
public sealed record SsoProviderRead(int Id, string Name, string Slug, string Issuer, string ClientId, bool HasSecret, string Scopes, bool Enabled, bool AutoCreateUsers)
{
    public static readonly Expression<Func<IdentityProvider, SsoProviderRead>> Projection =
        p => new SsoProviderRead(p.Id, p.Name, p.Slug, p.Issuer, p.ClientId, p.ClientSecret != null, p.Scopes, p.Enabled, p.AutoCreateUsers);
}

/// What the login page needs: which buttons to show.
public sealed record SsoProviderPublic(string Slug, string Name);
