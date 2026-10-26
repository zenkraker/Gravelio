using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Gravelio.Tests;

/// A small OpenID Connect provider on the loopback address: discovery, signing keys, the authorization endpoint (it "signs in" whoever the
/// test says), the token endpoint (it checks the client secret and the PKCE verifier) and user info. Just enough to drive the real handler.
public sealed class FakeIdp : IAsyncDisposable
{
    public const string ClientId = "gravelio-test";
    public const string ClientSecret = "s3cret-from-the-idp";

    readonly WebApplication app;
    readonly RSA rsa = RSA.Create(2048);
    readonly string kid = Guid.NewGuid().ToString("N");
    readonly Dictionary<string, Pending> codes = [];

    sealed record Pending(string Nonce, string Challenge, string RedirectUri, Dictionary<string, string> Claims);

    public string Issuer { get; }

    /// Who signs in the next time the authorization endpoint is visited.
    public Dictionary<string, string> NextUser { get; set; } = new() { ["sub"] = "sub-1" };

    /// Token requests that failed a check, with the reason.
    public List<string> Rejections { get; } = [];

    public FakeIdp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        app = builder.Build();
        var userInfo = new Dictionary<string, Dictionary<string, string>>();

        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = Issuer,
            ["authorization_endpoint"] = $"{Issuer}/authorize",
            ["token_endpoint"] = $"{Issuer}/token",
            ["userinfo_endpoint"] = $"{Issuer}/userinfo",
            ["jwks_uri"] = $"{Issuer}/jwks",
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
        }));
        app.MapGet("/jwks", () =>
        {
            var p = rsa.ExportParameters(false);
            return Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", kid, alg = "RS256", n = Base64UrlEncoder.Encode(p.Modulus), e = Base64UrlEncoder.Encode(p.Exponent) } } });
        });
        app.MapGet("/authorize", (HttpRequest r) =>
        {
            var code = Guid.NewGuid().ToString("N");
            codes[code] = new Pending(r.Query["nonce"]!, r.Query["code_challenge"]!, r.Query["redirect_uri"]!, new Dictionary<string, string>(NextUser));
            return Results.Redirect($"{r.Query["redirect_uri"]}?code={code}&state={Uri.EscapeDataString(r.Query["state"]!)}");
        });
        app.MapPost("/token", async (HttpRequest r) =>
        {
            var form = await r.ReadFormAsync();
            string? Reject(string why) { Rejections.Add(why); return null; }
            if (!codes.Remove(form["code"].ToString(), out var pending)) { Reject("unknown or reused code"); return Results.BadRequest(new { error = "invalid_grant" }); }
            if (form["client_id"] != ClientId || form["client_secret"] != ClientSecret) { Reject("bad client credentials"); return Results.Json(new { error = "invalid_client" }, statusCode: 401); }
            if (form["redirect_uri"] != pending.RedirectUri) { Reject("redirect_uri mismatch"); return Results.BadRequest(new { error = "invalid_grant" }); }
            var verifier = form["code_verifier"].ToString();
            if (verifier.Length == 0 || Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) != pending.Challenge)
            {
                Reject("PKCE verifier does not match the challenge");
                return Results.BadRequest(new { error = "invalid_grant" });
            }
            var accessToken = Guid.NewGuid().ToString("N");
            userInfo[accessToken] = pending.Claims;
            // The id token carries only the subject; the rest (email...) comes from user info, as many providers do.
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = ClientId,
                Expires = DateTime.UtcNow.AddMinutes(5),
                Subject = new ClaimsIdentity([new Claim("sub", pending.Claims["sub"]), new Claim("nonce", pending.Nonce)]),
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256),
            });
            return Results.Json(new { access_token = accessToken, token_type = "Bearer", expires_in = 300, id_token = token });
        });
        app.MapGet("/userinfo", (HttpRequest r) =>
            userInfo.TryGetValue(r.Headers.Authorization.ToString().Replace("Bearer ", ""), out var claims) ? Results.Json(claims) : Results.Unauthorized());

        app.Start();
        Issuer = app.Urls.First();
    }

    public async ValueTask DisposeAsync()
    {
        await app.DisposeAsync();
        rsa.Dispose();
    }
}
