using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

[Collection("api")]
public class SsoTests(ApiFixture api) : IAsyncLifetime
{
    readonly List<string> slugs = [];

    public Task InitializeAsync() => Task.CompletedTask;

    /// Providers are server-wide, so each test removes the ones it made: a leftover would change what the others see.
    public async Task DisposeAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.UserLogins.Where(l => slugs.Contains(l.LoginProvider)).ExecuteDeleteAsync();
        await db.IdentityProviders.Where(p => slugs.Contains(p.Slug)).ExecuteDeleteAsync();
    }

    const string Password = "Str0ng-pass!";

    /// A second host on the same database that may call the loopback identity provider.
    WebApplicationFactory<Program> LanFactory() => api.WithWebHostBuilder(b => b.UseSetting("Outbound:AllowPrivateNetworks", "true"));

    static HttpClient NoRedirects(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    static async Task<ApiClient> LoginOnAsync(WebApplicationFactory<Program> factory, string username, string password)
    {
        var client = new ApiClient(factory.CreateClient());
        await client.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = username, Password = password }, ApiClient.Json)).StatusCode);
        await client.SyncXsrfAsync();
        return client;
    }

    async Task<string> CreateProviderAsync(ApiClient admin, FakeIdp idp, bool autoCreate = false, string? slug = null)
    {
        slug ??= $"idp{Guid.NewGuid():N}"[..12];
        slugs.Add(slug);
        var response = await admin.Http.PostAsJsonAsync("/api/v1/sso-providers",
            new { Name = "Test IdP", Slug = slug, Issuer = idp.Issuer, ClientId = FakeIdp.ClientId, ClientSecret = FakeIdp.ClientSecret, AutoCreateUsers = autoCreate }, ApiClient.Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return slug;
    }

    /// Walks the whole browser round trip: our login (or link) address, the provider's authorize page, our callback, and our complete step.
    /// Returns where the last step sent the browser, and leaves the cookies in <paramref name="browser"/>.
    static async Task<string> BrowseAsync(HttpClient browser, string startPath)
    {
        var start = await browser.GetAsync(startPath);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var authorize = start.Headers.Location!;
        using var idpBrowser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var back = await idpBrowser.GetAsync(authorize);
        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        var callback = back.Headers.Location!;
        var cb = await browser.GetAsync(callback.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, cb.StatusCode);
        var location = cb.Headers.Location!.ToString();
        if (!location.Contains("/complete")) return location; // the handler refused it and sent the browser elsewhere
        var complete = await browser.GetAsync(cb.Headers.Location!.IsAbsoluteUri ? cb.Headers.Location.PathAndQuery : location);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        return complete.Headers.Location!.ToString();
    }

    static async Task<HttpStatusCode> MeAsync(HttpClient browser) => (await browser.GetAsync("/api/v1/users/me")).StatusCode;

    [Fact]
    public async Task A_person_links_their_account_and_then_signs_in_with_it()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp);
        var name = $"sso.link.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(await api.LoginAsync(), name, Password);

        // Signed in with a password, the person connects the identity provider ...
        var browser = NoRedirects(factory);
        var me = new ApiClient(browser);
        await me.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/v1/auth/login", new { Username = name, Password }, ApiClient.Json)).StatusCode);
        idp.NextUser = new() { ["sub"] = "sub-link-1", ["email"] = $"{name}@idp.example", ["email_verified"] = "true" };
        Assert.Equal("/security?sso=linked", await BrowseAsync(browser, $"/api/v1/users/me/sso/{slug}/link"));
        var mine = (await browser.GetFromJsonAsync<JsonElement[]>("/api/v1/users/me/sso", ApiClient.Json))!;
        Assert.True(mine.Single(p => p.GetProperty("slug").GetString() == slug).GetProperty("linked").GetBoolean());

        // ... and from then on signs in with it, in a browser that has no session.
        var other = NoRedirects(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(other));
        Assert.Equal("/", await BrowseAsync(other, $"/api/v1/auth/sso/{slug}/login"));
        var signedIn = await other.GetFromJsonAsync<JsonElement>("/api/v1/users/me", ApiClient.Json);
        Assert.Equal(name, signedIn.GetProperty("username").GetString());
        Assert.Empty(idp.Rejections);

        // The provider is needed again for the next sign-in: no session cookie is left from the external step.
        var third = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = "sub-someone-else" };
        Assert.Equal("/login?sso_error=not_linked", await BrowseAsync(third, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(third));
    }

    [Fact]
    public async Task A_provider_that_creates_accounts_does_so_only_for_a_verified_unused_email_and_never_links_by_email()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp, autoCreate: true);
        var tag = Guid.NewGuid().ToString("N")[..8];

        // A first sign-in makes an account, named from the provider's claims.
        var first = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = $"new-{tag}", ["email"] = $"new.{tag}@idp.example", ["email_verified"] = "true", ["preferred_username"] = $"Newcomer {tag}", ["name"] = "Nina Newcomer" };
        Assert.Equal("/", await BrowseAsync(first, $"/api/v1/auth/sso/{slug}/login"));
        var created = await first.GetFromJsonAsync<JsonElement>("/api/v1/users/me", ApiClient.Json);
        Assert.Equal($"new.{tag}@idp.example", created.GetProperty("email").GetString());
        Assert.Equal("Nina Newcomer", created.GetProperty("name").GetString());
        Assert.Equal("regular", created.GetProperty("access_type").GetString());
        var username = created.GetProperty("username").GetString()!;
        Assert.Matches("^[a-zA-Z0-9.]{1,45}$", username);

        // It has no password, so the password form cannot be used for it; the same identity signs in to the same account again.
        var plain = new ApiClient(api.CreateClient());
        await plain.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await plain.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = username, Password = "Anything-1!" }, ApiClient.Json)).StatusCode);
        var again = NoRedirects(factory);
        Assert.Equal("/", await BrowseAsync(again, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Equal(created.GetProperty("id").GetInt32(), (await again.GetFromJsonAsync<JsonElement>("/api/v1/users/me", ApiClient.Json)).GetProperty("id").GetInt32());

        // An email the provider has not verified makes no account.
        var unverified = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = $"unv-{tag}", ["email"] = $"unv.{tag}@idp.example", ["email_verified"] = "false" };
        Assert.Equal("/login?sso_error=email_unverified", await BrowseAsync(unverified, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(unverified));

        // An email that belongs to an existing account is not a way into it, even when the provider vouches for it.
        var local = $"sso.local.{tag}";
        var adminApi = await api.LoginAsync();
        await api.CreateUserAsync(adminApi, local, Password);
        var takeover = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = $"evil-{tag}", ["email"] = $"{local}@example.com", ["email_verified"] = "true" };
        Assert.Equal("/login?sso_error=email_in_use", await BrowseAsync(takeover, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(takeover));
    }

    [Fact]
    public async Task A_new_account_from_a_provider_waits_for_approval_when_the_server_asks_for_it()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp, autoCreate: true);
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { SignupEnabled = false, SignupRequireAdminApproval = true }, ApiClient.Json)).StatusCode);
            var tag = Guid.NewGuid().ToString("N")[..8];
            var browser = NoRedirects(factory);
            idp.NextUser = new() { ["sub"] = $"appr-{tag}", ["email"] = $"appr.{tag}@idp.example", ["email_verified"] = "true" };
            Assert.Equal("/login?sso_error=pending_approval", await BrowseAsync(browser, $"/api/v1/auth/sso/{slug}/login"));
            Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(browser));
            var pending = (await admin.GetJsonAsync<JsonElement>("/api/v1/users?page_size=100")).GetProperty("items").EnumerateArray()
                .Single(u => u.GetProperty("email").GetString() == $"appr.{tag}@idp.example");
            Assert.True(pending.GetProperty("pending_approval").GetBoolean());
        }
        finally
        {
            await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { SignupEnabled = false }, ApiClient.Json);
        }
    }

    [Fact]
    public async Task A_forged_or_replayed_answer_from_the_provider_is_refused()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp, autoCreate: true);
        var tag = Guid.NewGuid().ToString("N")[..8];
        idp.NextUser = new() { ["sub"] = $"forge-{tag}", ["email"] = $"forge.{tag}@idp.example", ["email_verified"] = "true" };

        // A callback with a state this browser never asked for (login CSRF) does nothing.
        var browser = NoRedirects(factory);
        var forged = await browser.GetAsync($"/api/v1/auth/sso/{slug}/callback?code=abc&state=forged");
        Assert.Equal(HttpStatusCode.Redirect, forged.StatusCode);
        Assert.Equal("/login?sso_error=failed", forged.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(browser));

        // The same answer cannot be used twice: the first use made the session, the second finds no code and no external cookie.
        var victim = NoRedirects(factory);
        var start = await victim.GetAsync($"/api/v1/auth/sso/{slug}/login");
        using var idpBrowser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var callback = (await idpBrowser.GetAsync(start.Headers.Location!)).Headers.Location!;
        Assert.Equal(HttpStatusCode.Redirect, (await victim.GetAsync(callback.PathAndQuery)).StatusCode);
        var replay = await NoRedirects(factory).GetAsync(callback.PathAndQuery); // another browser, no correlation cookie
        Assert.Equal("/login?sso_error=failed", replay.Headers.Location!.ToString());

        // Completing without having come through the provider is refused.
        var skipped = await NoRedirects(factory).GetAsync($"/api/v1/auth/sso/{slug}/complete");
        Assert.Equal("/login?sso_error=failed", skipped.Headers.Location!.ToString());
        // Unknown and disabled providers do not exist.
        Assert.Equal(HttpStatusCode.NotFound, (await NoRedirects(factory).GetAsync("/api/v1/auth/sso/nope/login")).StatusCode);
        Assert.Empty(idp.Rejections); // the identity provider itself never saw a bad token request
    }

    [Fact]
    public async Task Providers_are_admin_managed_and_the_secret_is_stored_protected_and_never_shown()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp);
        var listed = (await admin.GetJsonAsync<JsonElement>("/api/v1/sso-providers?page_size=100")).GetProperty("items").EnumerateArray().Single(p => p.GetProperty("slug").GetString() == slug);
        Assert.True(listed.GetProperty("has_secret").GetBoolean());
        Assert.DoesNotContain(FakeIdp.ClientSecret, listed.ToString());
        Assert.False(listed.TryGetProperty("client_secret", out _));
        using (var scope = api.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<AppDb>().IdentityProviders.Where(p => p.Slug == slug).Select(p => p.ClientSecret).SingleAsync();
            Assert.NotNull(stored);
            Assert.DoesNotContain(FakeIdp.ClientSecret, stored);
        }

        // Editing without a secret keeps it (the sign-in still works), a changed secret replaces it, and the address name cannot change.
        int id = listed.GetProperty("id").GetInt32();
        var edit = await admin.Http.PutAsJsonAsync($"/api/v1/sso-providers/{id}", new { Name = "Renamed", Slug = "ignored", Issuer = idp.Issuer, ClientId = FakeIdp.ClientId, AutoCreateUsers = true }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(slug, edited.GetProperty("slug").GetString());
        Assert.True(edited.GetProperty("has_secret").GetBoolean());
        var browser = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = "keep-1", ["email"] = $"keep.{slug}@idp.example", ["email_verified"] = "true" };
        Assert.Equal("/", await BrowseAsync(browser, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Empty(idp.Rejections);
        var wrong = await admin.Http.PutAsJsonAsync($"/api/v1/sso-providers/{id}", new { Name = "Renamed", Issuer = idp.Issuer, ClientId = FakeIdp.ClientId, ClientSecret = "now-wrong" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        idp.NextUser = new() { ["sub"] = "keep-2" };
        Assert.Equal("/login?sso_error=failed", await BrowseAsync(NoRedirects(factory), $"/api/v1/auth/sso/{slug}/login"));
        Assert.Contains("bad client credentials", idp.Rejections);

        // Only admins, valid addresses, and one provider per address name.
        var regular = await api.LoginAsync();
        var userName = $"sso.reg.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(regular, userName, Password);
        var plain = await api.LoginAsync(userName, Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.Http.GetAsync("/api/v1/sso-providers")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Http.PostAsJsonAsync("/api/v1/sso-providers", new { Name = "x", Slug = "Bad Slug!", Issuer = idp.Issuer, ClientId = "c" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Http.PostAsJsonAsync("/api/v1/sso-providers", new { Name = "x", Slug = "no-openid", Issuer = idp.Issuer, ClientId = "c", Scopes = "profile" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Http.PostAsJsonAsync("/api/v1/sso-providers", new { Name = "x", Slug = slug, Issuer = idp.Issuer, ClientId = "c" }, ApiClient.Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"/api/v1/sso-providers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await NoRedirects(factory).GetAsync($"/api/v1/auth/sso/{slug}/login")).StatusCode);
    }

    [Fact]
    public async Task An_issuer_without_https_is_refused_and_one_that_points_inside_the_network_is_never_fetched()
    {
        await using var idp = new FakeIdp();
        var admin = await api.LoginAsync(); // the default host: private networks are not allowed
        var http = await admin.Http.PostAsJsonAsync("/api/v1/sso-providers", new { Name = "Plain", Slug = $"plain{Guid.NewGuid():N}"[..12], Issuer = idp.Issuer, ClientId = "c" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.BadRequest, http.StatusCode);
        Assert.Contains("https", await http.Content.ReadAsStringAsync());

        // https, but a name that resolves to the loopback address: the sign-in cannot start, and nothing is sent there.
        var slug = $"inside{Guid.NewGuid():N}"[..12];
        slugs.Add(slug);
        Assert.Equal(HttpStatusCode.Created, (await admin.Http.PostAsJsonAsync("/api/v1/sso-providers", new { Name = "Inside", Slug = slug, Issuer = "https://localhost:9/realm", ClientId = "c" }, ApiClient.Json)).StatusCode);
        try
        {
            using var browser = api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var start = await browser.GetAsync($"/api/v1/auth/sso/{slug}/login");
            Assert.Equal("/login?sso_error=unavailable", start.Headers.Location!.ToString());
        }
        finally
        {
            var id = (await admin.GetJsonAsync<JsonElement>("/api/v1/sso-providers?page_size=100")).GetProperty("items").EnumerateArray().Single(p => p.GetProperty("slug").GetString() == slug).GetProperty("id").GetInt32();
            await admin.Http.DeleteAsync($"/api/v1/sso-providers/{id}");
        }
    }

    [Fact]
    public async Task Password_sign_in_can_be_turned_off_for_regular_users_while_a_provider_works_and_admins_keep_it()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var name = $"sso.off.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(await api.LoginAsync(), name, Password);

        // Nobody could sign in then, so it is refused until a provider is there.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { LocalLoginEnabled = false }, ApiClient.Json)).StatusCode);

        var slug = await CreateProviderAsync(admin, idp);
        int id = (await admin.GetJsonAsync<JsonElement>("/api/v1/sso-providers?page_size=100")).GetProperty("items").EnumerateArray().Single(p => p.GetProperty("slug").GetString() == slug).GetProperty("id").GetInt32();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { LocalLoginEnabled = false, SsoAutoRedirect = true }, ApiClient.Json)).StatusCode);
            var anon = new ApiClient(factory.CreateClient());
            await anon.SyncXsrfAsync();
            var visible = await anon.GetJsonAsync<JsonElement>("/api/v1/server-settings/public");
            Assert.False(visible.GetProperty("local_login_enabled").GetBoolean());
            Assert.True(visible.GetProperty("sso_auto_redirect").GetBoolean());
            Assert.Equal(slug, Assert.Single(visible.GetProperty("sso_providers").EnumerateArray()).GetProperty("slug").GetString());

            var right = await anon.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = name, Password }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
            Assert.Contains("single sign-on", await right.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = name, Password = "Wrong-pass-1!" }, ApiClient.Json)).StatusCode);
            await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword); // an admin still can

            // The last provider cannot be switched off or removed meanwhile.
            Assert.Equal(HttpStatusCode.Conflict, (await admin.Http.PutAsJsonAsync($"/api/v1/sso-providers/{id}", new { Name = "x", Issuer = idp.Issuer, ClientId = "c", Enabled = false }, ApiClient.Json)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.Http.DeleteAsync($"/api/v1/sso-providers/{id}")).StatusCode);
        }
        finally
        {
            await admin.Http.PutAsJsonAsync("/api/v1/server-settings", new { LocalLoginEnabled = true }, ApiClient.Json);
            await admin.Http.DeleteAsync($"/api/v1/sso-providers/{id}");
        }
        await api.LoginAsync(name, Password); // password sign-in is back
    }

    [Fact]
    public async Task The_only_way_to_sign_in_cannot_be_unlinked()
    {
        await using var idp = new FakeIdp();
        using var factory = LanFactory();
        var admin = await LoginOnAsync(factory, "admin", ApiFixture.AdminPassword);
        var slug = await CreateProviderAsync(admin, idp, autoCreate: true);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var browser = NoRedirects(factory);
        idp.NextUser = new() { ["sub"] = $"only-{tag}", ["email"] = $"only.{tag}@idp.example", ["email_verified"] = "true" };
        Assert.Equal("/", await BrowseAsync(browser, $"/api/v1/auth/sso/{slug}/login"));
        var me = new ApiClient(browser);
        await me.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await browser.DeleteAsync($"/api/v1/users/me/sso/{slug}")).StatusCode);

        // A person with a password can unlink; the identity then signs in to nothing.
        var withPassword = $"sso.unl.{tag}";
        await api.CreateUserAsync(await api.LoginAsync(), withPassword, Password);
        var other = NoRedirects(factory);
        var otherApi = new ApiClient(other);
        await otherApi.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await other.PostAsJsonAsync("/api/v1/auth/login", new { Username = withPassword, Password }, ApiClient.Json)).StatusCode);
        await otherApi.SyncXsrfAsync();
        idp.NextUser = new() { ["sub"] = $"pw-{tag}" };
        Assert.Equal("/security?sso=linked", await BrowseAsync(other, $"/api/v1/users/me/sso/{slug}/link"));
        Assert.Equal(HttpStatusCode.NoContent, (await other.DeleteAsync($"/api/v1/users/me/sso/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/users/me/sso/{slug}")).StatusCode);
        // The identity no longer reaches the account: this provider would make a new one, but it gave no verified email.
        var after = NoRedirects(factory);
        Assert.Equal("/login?sso_error=email_unverified", await BrowseAsync(after, $"/api/v1/auth/sso/{slug}/login"));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(after));
    }
}
