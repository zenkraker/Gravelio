using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Tests;

[Collection("api")]
public class ConcurrencyTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    static HttpRequestMessage Put(string url, object body, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body, options: ApiClient.Json) };
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    [Fact]
    public async Task A_stale_activity_edit_is_refused_instead_of_overwriting()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "etag.activity", Password);
        var me = await api.LoginAsync("etag.activity", Password);
        var upload = await me.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("e.gpx", TestData.Gpx("Edited twice", new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc))));
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single().GetProperty("id").GetInt32();
        object Body(string name) => new { Name = name, ActivityType = 1, Visibility = Visibilities.Private };

        var first = await me.Http.GetAsync($"/api/v1/activities/{id}");
        var tag = first.Headers.ETag!.ToString();

        // Two tabs hold the same version; the first save wins and moves the version on.
        var saved = await me.Http.SendAsync(Put($"/api/v1/activities/{id}", Body("Tab one"), tag));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.NotEqual(tag, saved.Headers.ETag!.ToString());

        var stale = await me.Http.SendAsync(Put($"/api/v1/activities/{id}", Body("Tab two"), tag));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Tab one", (await me.GetJsonAsync<JsonElement>($"/api/v1/activities/{id}")).GetProperty("name").GetString());

        // The version a save returns is the one the next save may use, and a client that sends no version still works.
        var again = await me.Http.SendAsync(Put($"/api/v1/activities/{id}", Body("Tab one again"), saved.Headers.ETag!.ToString()));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await me.Http.SendAsync(Put($"/api/v1/activities/{id}", Body("No header"), null))).StatusCode);
    }

    [Fact]
    public async Task A_stale_gear_edit_is_refused_instead_of_overwriting()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "etag.gear", Password);
        var me = await api.LoginAsync("etag.gear", Password);
        var created = await me.Http.PostAsJsonAsync("/api/v1/gears", new { Nickname = "Road bike", GearType = 1, InitialDistance = 0 }, ApiClient.Json);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        object Body(string name) => new { Nickname = name, GearType = 1, Active = true, InitialDistance = 0 };

        var tag = (await me.Http.GetAsync($"/api/v1/gears/{id}")).Headers.ETag!.ToString();
        Assert.Equal(HttpStatusCode.OK, (await me.Http.SendAsync(Put($"/api/v1/gears/{id}", Body("Gravel bike"), tag))).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await me.Http.SendAsync(Put($"/api/v1/gears/{id}", Body("Mountain bike"), tag))).StatusCode);
        Assert.Equal("Gravel bike", (await me.GetJsonAsync<JsonElement>($"/api/v1/gears/{id}")).GetProperty("nickname").GetString());
    }
}
