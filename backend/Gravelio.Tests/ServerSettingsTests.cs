using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gravelio.Tests;

[Collection("api")]
public class ServerSettingsTests(ApiFixture api)
{
    static readonly Regex VerifyLink = new(@"user=(\d+)&token=(\S+)");

    static Task<HttpResponseMessage> SaveAsync(ApiClient admin, bool signup, bool verify = false, bool approval = false) =>
        admin.Http.PutAsJsonAsync("/api/v1/server-settings",
            new { SignupEnabled = signup, SignupRequireEmailVerification = verify, SignupRequireAdminApproval = approval },
            ApiClient.Json);

    async Task<ApiClient> AnonymousAsync()
    {
        var client = new ApiClient(api.CreateClient());
        await client.SyncXsrfAsync();
        return client;
    }

    static Task<HttpResponseMessage> SignupAsync(ApiClient client, string username, string password = "Signup-Pass-1!") =>
        client.Http.PostAsJsonAsync("/api/v1/auth/signup",
            new { Name = "New Person", Username = username, Email = $"{username}@example.com", Password = password },
            ApiClient.Json);

    [Fact]
    public async Task Settings_default_to_no_signup_and_are_admin_only()
    {
        var admin = await api.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(admin, false)).StatusCode);

        var settings = await admin.GetJsonAsync<JsonElement>("/api/v1/server-settings");
        Assert.False(settings.GetProperty("signup_enabled").GetBoolean());

        var anon = await AnonymousAsync();
        var publicSettings = await anon.GetJsonAsync<JsonElement>("/api/v1/server-settings/public");
        Assert.False(publicSettings.GetProperty("signup_enabled").GetBoolean());
        // Public settings carry only what a visitor needs.
        Assert.Equal(new[] { "public_shareable_links", "signup_enabled" }, publicSettings.EnumerateObject().Select(p => p.Name).Order());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Http.GetAsync("/api/v1/server-settings")).StatusCode);
        var regularName = $"settings.regular.{Guid.NewGuid():N}"[..24];
        await api.CreateUserAsync(admin, regularName);
        var regular = await api.LoginAsync(regularName, "Str0ng-pass!");
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.Http.GetAsync("/api/v1/server-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.Http.PutAsJsonAsync("/api/v1/server-settings",
            new { SignupEnabled = true, SignupRequireEmailVerification = false, SignupRequireAdminApproval = false }, ApiClient.Json)).StatusCode);

        // Disabled sign-up is refused to everyone.
        var anon2 = await AnonymousAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await SignupAsync(anon2, $"signup.off.{Guid.NewGuid():N}"[..20])).StatusCode);
    }

    [Fact]
    public async Task Enabled_signup_creates_a_working_account()
    {
        var admin = await api.LoginAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SaveAsync(admin, true)).StatusCode);
            var anon = await AnonymousAsync();
            Assert.True((await anon.GetJsonAsync<JsonElement>("/api/v1/server-settings/public")).GetProperty("signup_enabled").GetBoolean());

            var username = $"signup.open.{Guid.NewGuid():N}"[..21];
            var response = await SignupAsync(anon, username);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
            Assert.False(result.GetProperty("pending_email_verification").GetBoolean());
            Assert.False(result.GetProperty("pending_approval").GetBoolean());

            await api.LoginAsync(username, "Signup-Pass-1!");

            // The usual Identity conflicts still apply.
            var duplicate = await SignupAsync(anon, username);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }
        finally
        {
            await SaveAsync(admin, false);
        }
    }

    [Fact]
    public async Task Signup_with_email_verification_stays_locked_until_the_link_is_used()
    {
        var admin = await api.LoginAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SaveAsync(admin, true, verify: true)).StatusCode);
            api.Email.Clear();
            var anon = await AnonymousAsync();
            var username = $"signup.verify.{Guid.NewGuid():N}"[..23];

            var response = await SignupAsync(anon, username);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
            Assert.True(result.GetProperty("pending_email_verification").GetBoolean());

            var login = await anon.Http.PostAsJsonAsync("/api/v1/auth/login",
                new { Username = username, Password = "Signup-Pass-1!" }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
            Assert.Contains("Confirm your email", await login.Content.ReadAsStringAsync());

            var message = Assert.Single(api.Email.Sent, m => m.To == $"{username}@example.com");
            var match = VerifyLink.Match(message.Body);
            Assert.True(match.Success, message.Body);
            int userId = int.Parse(match.Groups[1].Value);
            var token = Uri.UnescapeDataString(match.Groups[2].Value);

            var verify = await anon.Http.PostAsJsonAsync("/api/v1/auth/verify-email", new { UserId = userId, Token = token }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
            await api.LoginAsync(username, "Signup-Pass-1!");

            // Single use.
            Assert.Equal(HttpStatusCode.BadRequest,
                (await anon.Http.PostAsJsonAsync("/api/v1/auth/verify-email", new { UserId = userId, Token = token }, ApiClient.Json)).StatusCode);
        }
        finally
        {
            await SaveAsync(admin, false);
        }
    }

    [Fact]
    public async Task Signup_with_admin_approval_waits_for_an_admin_to_activate_it()
    {
        var admin = await api.LoginAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SaveAsync(admin, true, approval: true)).StatusCode);
            var anon = await AnonymousAsync();
            var username = $"signup.approve.{Guid.NewGuid():N}"[..24];

            var response = await SignupAsync(anon, username);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
            Assert.True(result.GetProperty("pending_approval").GetBoolean());

            var login = await anon.Http.PostAsJsonAsync("/api/v1/auth/login",
                new { Username = username, Password = "Signup-Pass-1!" }, ApiClient.Json);
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

            var page = await admin.GetJsonAsync<JsonElement>("/api/v1/users?page_size=100");
            int id = page.GetProperty("items").EnumerateArray()
                .Single(u => u.GetProperty("username").GetString() == username).GetProperty("id").GetInt32();
            var activate = await admin.Http.PutAsJsonAsync($"/api/v1/users/{id}",
                new { Name = "New Person", Username = username, Email = $"{username}@example.com", AccessType = "regular", Active = true },
                ApiClient.Json);
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

            await api.LoginAsync(username, "Signup-Pass-1!");
        }
        finally
        {
            await SaveAsync(admin, false);
        }
    }
}
