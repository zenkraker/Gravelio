using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Sessions;

namespace Gravelio.Tests;

[Collection("api")]
public class SessionTests(ApiFixture api)
{
    static async Task<List<JsonElement>> ListAsync(ApiClient client, string url = "/api/v1/users/me/sessions") =>
        (await client.GetJsonAsync<JsonElement>(url + (url.Contains('?') ? "&" : "?") + "page_size=100")).GetProperty("items").EnumerateArray().ToList();

    [Theory]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36", "Chrome on Linux")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0 Safari/537.36 Edg/130.0", "Edge on Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile Safari/604.1", "Safari on iOS")]
    [InlineData("Mozilla/5.0 (Android 14; Mobile; rv:130.0) Gecko/130.0 Firefox/130.0", "Firefox on Android")]
    [InlineData("", "Unknown device")]
    public void The_device_label_names_browser_and_system(string userAgent, string expected) =>
        Assert.Equal(expected, UserAgentInfo.Describe(userAgent));

    [Fact]
    public async Task A_sign_in_is_listed_with_its_device_and_marked_as_current()
    {
        var admin = await api.LoginAsync();
        var name = $"sess.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(admin, name);
        var user = new ApiClient(api.CreateClient());
        user.Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) Chrome/130.0 Safari/537.36");
        await user.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = name, Password = "Str0ng-pass!" }, ApiClient.Json)).StatusCode);
        await user.SyncXsrfAsync();

        var session = Assert.Single(await ListAsync(user));
        Assert.Equal("Chrome on Linux", session.GetProperty("device").GetString());
        Assert.True(session.GetProperty("current").GetBoolean());
        // The ticket (claims, security stamp) is never part of what the list shows.
        Assert.DoesNotContain("ticket", session.ToString());
    }

    [Fact]
    public async Task Ending_another_device_signs_it_out_and_leaves_the_caller_signed_in()
    {
        var admin = await api.LoginAsync();
        var name = $"sess.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(admin, name);
        var phone = await api.LoginAsync(name, "Str0ng-pass!");
        var laptop = await api.LoginAsync(name, "Str0ng-pass!");

        var sessions = await ListAsync(laptop);
        Assert.Equal(2, sessions.Count);
        var other = sessions.Single(s => !s.GetProperty("current").GetBoolean()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await laptop.Http.DeleteAsync($"/api/v1/users/me/sessions/{other}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Single(await ListAsync(laptop));
        Assert.Equal(HttpStatusCode.NotFound, (await laptop.Http.DeleteAsync($"/api/v1/users/me/sessions/{other}")).StatusCode);
    }

    [Fact]
    public async Task Nobody_can_see_or_end_another_users_session()
    {
        var admin = await api.LoginAsync();
        var a = $"sess.{Guid.NewGuid():N}"[..14];
        var b = $"sess.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(admin, a);
        await api.CreateUserAsync(admin, b);
        var alice = await api.LoginAsync(a, "Str0ng-pass!");
        var bob = await api.LoginAsync(b, "Str0ng-pass!");

        var aliceSession = (await ListAsync(alice)).Single().GetProperty("id").GetInt32();
        Assert.DoesNotContain(await ListAsync(bob), s => s.GetProperty("id").GetInt32() == aliceSession);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.DeleteAsync($"/api/v1/users/me/sessions/{aliceSession}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.Http.GetAsync("/api/v1/users/me")).StatusCode);
        // The admin routes are for admins only.
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.Http.GetAsync("/api/v1/users/1/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.Http.DeleteAsync($"/api/v1/users/1/sessions/{aliceSession}")).StatusCode);
    }

    [Fact]
    public async Task An_admin_can_list_and_end_a_users_sessions()
    {
        var admin = await api.LoginAsync();
        var name = $"sess.{Guid.NewGuid():N}"[..14];
        int id = await api.CreateUserAsync(admin, name);
        var user = await api.LoginAsync(name, "Str0ng-pass!");

        var session = Assert.Single(await ListAsync(admin, $"/api/v1/users/{id}/sessions"));
        Assert.False(session.GetProperty("current").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"/api/v1/users/{id}/sessions/{session.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Http.GetAsync("/api/v1/users/me")).StatusCode);
        // The session belongs to that user only: the wrong user id finds nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Http.DeleteAsync($"/api/v1/users/1/sessions/{session.GetProperty("id").GetInt32()}")).StatusCode);
    }

    [Fact]
    public async Task Signing_out_removes_the_session()
    {
        var admin = await api.LoginAsync();
        var name = $"sess.{Guid.NewGuid():N}"[..14];
        int id = await api.CreateUserAsync(admin, name);
        var user = await api.LoginAsync(name, "Str0ng-pass!");
        Assert.Single(await ListAsync(admin, $"/api/v1/users/{id}/sessions"));

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Empty(await ListAsync(admin, $"/api/v1/users/{id}/sessions"));
    }

    [Fact]
    public async Task A_password_change_ends_the_other_devices()
    {
        var admin = await api.LoginAsync();
        var name = $"sess.{Guid.NewGuid():N}"[..14];
        await api.CreateUserAsync(admin, name);
        var phone = await api.LoginAsync(name, "Str0ng-pass!");
        var laptop = await api.LoginAsync(name, "Str0ng-pass!");

        var change = await laptop.Http.PutAsJsonAsync("/api/v1/users/me/password", new { CurrentPassword = "Str0ng-pass!", NewPassword = "N3w-Str0ng-pass!" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }
}
