using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Gravelio.Tests;

[Collection("api")]
public class PasswordResetTests(ApiFixture api)
{
    static readonly Regex TokenPattern = new(@"[?&]token=([A-Za-z0-9_-]+)");

    async Task<(string Username, string Email)> NewUserAsync(ApiClient admin)
    {
        var username = $"reset.{Guid.NewGuid():N}"[..20];
        await api.CreateUserAsync(admin, username, "Old-Password-1!");
        return (username, $"{username}@example.com");
    }

    static async Task<ApiClient> AnonymousAsync(ApiFixture api)
    {
        var client = new ApiClient(api.CreateClient());
        await client.SyncXsrfAsync();
        return client;
    }

    async Task<string> RequestTokenAsync(ApiClient client, string email)
    {
        api.Email.Clear();
        var response = await client.Http.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = email }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(api.Email.Sent, m => m.To == email);
        return TokenPattern.Match(message.Body).Groups[1].Value;
    }

    static Task<HttpResponseMessage> ResetAsync(ApiClient client, string token, string password) =>
        client.Http.PostAsJsonAsync("/api/v1/auth/reset-password", new { Token = token, NewPassword = password }, ApiClient.Json);

    [Fact]
    public async Task Reset_link_changes_the_password_and_cannot_be_replayed()
    {
        var admin = await api.LoginAsync();
        var (username, email) = await NewUserAsync(admin);
        var client = await AnonymousAsync(api);

        var token = await RequestTokenAsync(client, email);
        Assert.NotEmpty(token);

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, token, "New-Password-1!")).StatusCode);
        // The old password is gone and the new one signs in.
        await api.LoginAsync(username, "New-Password-1!");
        var oldPassword = await AnonymousAsync(api);
        var oldLogin = await oldPassword.Http.PostAsJsonAsync("/api/v1/auth/login",
            new { Username = username, Password = "Old-Password-1!" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // Single use.
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(client, token, "Another-Password-2!")).StatusCode);
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_and_no_message()
    {
        var admin = await api.LoginAsync();
        await NewUserAsync(admin);
        var client = await AnonymousAsync(api);
        api.Email.Clear();

        var response = await client.Http.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { Email = "nobody@example.com" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(api.Email.Sent);
    }

    [Fact]
    public async Task A_rejected_weak_password_keeps_the_link_usable()
    {
        var admin = await api.LoginAsync();
        var (username, email) = await NewUserAsync(admin);
        var client = await AnonymousAsync(api);

        var token = await RequestTokenAsync(client, email);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(client, token, "short")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, token, "New-Password-1!")).StatusCode);
        await api.LoginAsync(username, "New-Password-1!");
    }

    [Fact]
    public async Task A_second_request_invalidates_the_first_link()
    {
        var admin = await api.LoginAsync();
        var (_, email) = await NewUserAsync(admin);
        var client = await AnonymousAsync(api);

        var first = await RequestTokenAsync(client, email);
        var second = await RequestTokenAsync(client, email);
        Assert.NotEqual(first, second);

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(client, first, "New-Password-1!")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, second, "New-Password-1!")).StatusCode);
    }
}
