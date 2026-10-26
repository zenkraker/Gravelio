using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class MfaTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    /// RFC 6238 with the 30-second step authenticator apps use. Identity accepts the codes of two steps either side of now,
    /// so a code from a step or two ahead is a valid code that has not been used yet.
    static string Code(string base32Secret, int stepsAhead = 0)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(base32Secret.ToUpperInvariant().TrimEnd('=').Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        var key = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + stepsAhead;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    record Enabled(ApiClient Client, string Secret, string[] RecoveryCodes);

    /// Creates a user, signs in, and turns MFA on with the code of the current step.
    async Task<Enabled> EnableMfaAsync(string username)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        var user = await api.LoginAsync(username, Password);
        var setup = await user.Http.PostAsync("/api/v1/users/me/mfa/setup", null);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var secret = (await setup.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("secret").GetString()!;
        var enable = await user.Http.PostAsJsonAsync("/api/v1/users/me/mfa/enable", new { CurrentPassword = Password, Code = Code(secret) }, ApiClient.Json);
        Assert.True(enable.StatusCode == HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());
        var codes = (await enable.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("recovery_codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        await user.SyncXsrfAsync();
        return new Enabled(user, secret, codes);
    }

    /// The first half of signing in: the password. The result is a client holding only the short-lived two-factor cookie.
    async Task<ApiClient> PasswordStepAsync(string username)
    {
        var client = new ApiClient(api.CreateClient());
        await client.SyncXsrfAsync();
        var login = await client.Http.PostAsJsonAsync("/api/v1/auth/login", new { Username = username, Password }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("mfa_required").GetBoolean());
        return client;
    }

    static Task<HttpResponseMessage> VerifyAsync(ApiClient client, string code) =>
        client.Http.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { Code = code }, ApiClient.Json);

    [Fact]
    public async Task Turning_MFA_on_needs_the_password_and_a_code_from_the_new_key_and_returns_recovery_codes()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "mfa.on", Password);
        var user = await api.LoginAsync("mfa.on", Password);
        Assert.False((await user.GetJsonAsync<JsonElement>("/api/v1/users/me/mfa")).GetProperty("enabled").GetBoolean());

        var secret = (await (await user.Http.PostAsync("/api/v1/users/me/mfa/setup", null)).Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("secret").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PostAsJsonAsync("/api/v1/users/me/mfa/enable", new { CurrentPassword = Password, Code = "000000" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PostAsJsonAsync("/api/v1/users/me/mfa/enable", new { CurrentPassword = "wrong", Code = Code(secret) }, ApiClient.Json)).StatusCode);

        var enable = await user.Http.PostAsJsonAsync("/api/v1/users/me/mfa/enable", new { CurrentPassword = Password, Code = Code(secret) }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        Assert.Equal(10, (await enable.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("recovery_codes").GetArrayLength());
        await user.SyncXsrfAsync();

        var status = await user.GetJsonAsync<JsonElement>("/api/v1/users/me/mfa");
        Assert.True(status.GetProperty("enabled").GetBoolean());
        Assert.Equal(10, status.GetProperty("recovery_codes_left").GetInt32());
    }

    [Fact]
    public async Task An_account_with_MFA_has_no_session_until_the_code_is_given()
    {
        var mfa = await EnableMfaAsync("mfa.second");
        var client = await PasswordStepAsync("mfa.second");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Http.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(client, "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await VerifyAsync(client, Code(mfa.Secret, 1))).StatusCode);
        await client.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.Http.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task An_authenticator_code_works_once()
    {
        var mfa = await EnableMfaAsync("mfa.once");
        var code = Code(mfa.Secret, 1);

        Assert.Equal(HttpStatusCode.NoContent, (await VerifyAsync(await PasswordStepAsync("mfa.once"), code)).StatusCode);
        // The same valid code, even from another sign-in, is refused the second time.
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await PasswordStepAsync("mfa.once"), code)).StatusCode);
        // The code that confirmed MFA during setup counts as used too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await PasswordStepAsync("mfa.once"), Code(mfa.Secret))).StatusCode);
        // A different valid code still signs in.
        Assert.Equal(HttpStatusCode.NoContent, (await VerifyAsync(await PasswordStepAsync("mfa.once"), Code(mfa.Secret, 2))).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once()
    {
        var mfa = await EnableMfaAsync("mfa.recovery");

        var first = await PasswordStepAsync("mfa.recovery");
        Assert.Equal(HttpStatusCode.NoContent, (await VerifyAsync(first, mfa.RecoveryCodes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await PasswordStepAsync("mfa.recovery"), mfa.RecoveryCodes[0])).StatusCode);
        await mfa.Client.SyncXsrfAsync();
        Assert.Equal(9, (await mfa.Client.GetJsonAsync<JsonElement>("/api/v1/users/me/mfa")).GetProperty("recovery_codes_left").GetInt32());
    }

    [Fact]
    public async Task Wrong_codes_lock_the_account()
    {
        var mfa = await EnableMfaAsync("mfa.lock");
        var client = await PasswordStepAsync("mfa.lock");

        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(client, "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await VerifyAsync(client, Code(mfa.Secret, 1))).StatusCode);
    }

    [Fact]
    public async Task Turning_MFA_off_needs_the_password_and_a_code_and_makes_the_old_codes_useless()
    {
        var mfa = await EnableMfaAsync("mfa.off");

        Assert.Equal(HttpStatusCode.BadRequest, (await mfa.Client.Http.PostAsJsonAsync("/api/v1/users/me/mfa/disable", new { CurrentPassword = Password, Code = "000000" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mfa.Client.Http.PostAsJsonAsync("/api/v1/users/me/mfa/disable", new { CurrentPassword = Password, Code = Code(mfa.Secret, 1) }, ApiClient.Json)).StatusCode);

        await api.LoginAsync("mfa.off", Password); // a plain sign-in again
        var after = await api.LoginAsync("mfa.off", Password);
        Assert.False((await after.GetJsonAsync<JsonElement>("/api/v1/users/me/mfa")).GetProperty("enabled").GetBoolean());
        Assert.Equal(0, (await after.GetJsonAsync<JsonElement>("/api/v1/users/me/mfa")).GetProperty("recovery_codes_left").GetInt32());
    }

    [Fact]
    public async Task Changing_the_password_needs_a_code_while_MFA_is_on()
    {
        var mfa = await EnableMfaAsync("mfa.password");

        Assert.Equal(HttpStatusCode.BadRequest, (await mfa.Client.Http.PutAsJsonAsync("/api/v1/users/me/password", new { CurrentPassword = Password, NewPassword = "N3w-passw0rd!" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mfa.Client.Http.PutAsJsonAsync("/api/v1/users/me/password", new { CurrentPassword = Password, NewPassword = "N3w-passw0rd!", Code = Code(mfa.Secret, 1) }, ApiClient.Json)).StatusCode);
    }

    [Fact]
    public async Task New_recovery_codes_replace_the_old_ones()
    {
        var mfa = await EnableMfaAsync("mfa.regen");

        var regenerate = await mfa.Client.Http.PostAsJsonAsync("/api/v1/users/me/mfa/recovery-codes", new { CurrentPassword = Password, Code = mfa.RecoveryCodes[0] }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, regenerate.StatusCode);
        var fresh = (await regenerate.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("recovery_codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Equal(10, fresh.Length);
        Assert.DoesNotContain(mfa.RecoveryCodes[1], fresh);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(await PasswordStepAsync("mfa.regen"), mfa.RecoveryCodes[1])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await VerifyAsync(await PasswordStepAsync("mfa.regen"), fresh[0])).StatusCode);
    }
}
