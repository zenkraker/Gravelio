using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Gravelio.Tests;

[Collection("api")]
public class DataTransferTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";
    static readonly DateTime Start = new(2025, 5, 3, 8, 0, 0, DateTimeKind.Utc);

    async Task<ApiClient> UserAsync(string username)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        return await api.LoginAsync(username, Password);
    }

    /// A person with one of everything the export carries.
    async Task<ApiClient> RichUserAsync(string username)
    {
        var user = await UserAsync(username);
        var gear = await user.Http.PostAsJsonAsync("/api/v1/gears", new { Nickname = "Export bike", Brand = "Canyon", GearType = 1, InitialDistance = 1000.0 }, ApiClient.Json);
        var gearId = (await gear.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync($"/api/v1/gears/{gearId}/components", new { Type = "chain", PurchaseDate = "2025-01-01" }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Http.PutAsJsonAsync("/api/v1/gears/defaults/4", new { GearId = gearId }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync("/api/v1/goals", new { Interval = "weekly", ActivityGroup = "bike", Metric = "distance", Target = 100_000 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync("/api/v1/health/weight", new { Date = "2025-05-01", Weight = 80.5m }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync("/api/v1/health/steps", new { Date = "2025-05-01", Steps = 9000 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await user.Http.PostAsJsonAsync("/api/v1/health/water", new { Date = "2025-05-01", AmountMl = 500m }, ApiClient.Json)).StatusCode);

        var upload = await user.Http.PostAsync("/api/v1/activities/upload", TestData.Upload("a.gpx", TestData.Gpx("Exported ride", Start, sport: "cycling", heartRate: true)));
        var activity = (await upload.Content.ReadFromJsonAsync<JsonElement[]>(ApiClient.Json))!.Single();
        var id = activity.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await user.Http.PutAsJsonAsync($"/api/v1/activities/{id}", new { Name = "Exported ride", Description = "Sunny", ActivityType = 4, Visibility = 0 }, ApiClient.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Http.PutAsJsonAsync($"/api/v1/activities/{id}/private", new { PrivateNotes = "Legs were heavy", WorkoutFeeling = 60 }, ApiClient.Json)).StatusCode);
        return user;
    }

    static async Task<byte[]> ExportAsync(ApiClient user)
    {
        var response = await user.Http.GetAsync("/api/v1/users/me/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsByteArrayAsync();
    }

    static Task<HttpResponseMessage> ImportAsync(ApiClient user, byte[] zip, string fileName = "export.zip")
    {
        var file = new ByteArrayContent(zip);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        return user.Http.PostAsync("/api/v1/users/me/import", new MultipartFormDataContent { { file, "file", fileName } });
    }

    static byte[] MakeZip(params (string Name, string Json)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, json) in entries)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                w.Write(json);
            }
        return ms.ToArray();
    }

    [Fact]
    public async Task An_export_holds_the_persons_data_and_no_secrets()
    {
        var user = await RichUserAsync("xfer.export");
        await RichUserAsync("xfer.bystander"); // someone else's data must not leak in
        var bytes = await ExportAsync(user);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(["activities/1.json", "data.json", "manifest.json"], zip.Entries.Select(e => e.FullName).Order());
        var everything = string.Concat(zip.Entries.Select(e => new StreamReader(e.Open()).ReadToEnd()));
        Assert.Contains("Exported ride", everything);
        Assert.Contains("Legs were heavy", everything); // the owner's private notes are theirs to export
        Assert.Contains("Export bike", everything);
        Assert.DoesNotContain("password", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("security_stamp", everything);
        Assert.DoesNotContain("two_factor", everything);
        Assert.DoesNotContain("xfer.bystander", everything);
        Assert.Equal("gravelio-export", JsonDocument.Parse(new StreamReader(zip.GetEntry("manifest.json")!.Open()).ReadToEnd()).RootElement.GetProperty("format").GetString());
    }

    [Fact]
    public async Task An_export_imports_into_another_account_and_importing_it_again_changes_nothing()
    {
        var source = await RichUserAsync("xfer.source");
        var bytes = await ExportAsync(source);
        var target = await UserAsync("xfer.target");

        var first = await ImportAsync(target, bytes);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(1, result.GetProperty("gears").GetInt32());
        Assert.Equal(1, result.GetProperty("goals").GetInt32());
        Assert.Equal(3, result.GetProperty("health_entries").GetInt32());
        Assert.Equal(1, result.GetProperty("activities_imported").GetInt32());

        var activities = await target.GetJsonAsync<JsonElement>("/api/v1/activities");
        var imported = activities.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Exported ride", imported.GetProperty("name").GetString());
        var importedId = imported.GetProperty("id").GetInt32();
        Assert.NotEqual(JsonValueKind.Null, imported.GetProperty("gear_id").ValueKind);
        Assert.Equal("Legs were heavy", (await target.GetJsonAsync<JsonElement>($"/api/v1/activities/{importedId}/private")).GetProperty("private_notes").GetString());
        Assert.Contains(1, (await target.GetJsonAsync<JsonElement[]>($"/api/v1/activities/{importedId}/streams")).Select(s => s.GetProperty("stream_type").GetInt32()));
        var gears = (await target.GetJsonAsync<JsonElement>("/api/v1/gears")).GetProperty("items");
        var gearId = gears.EnumerateArray().Single().GetProperty("id").GetInt32();
        Assert.Single(await target.GetJsonAsync<JsonElement[]>($"/api/v1/gears/{gearId}/components"));
        Assert.Equal(gearId, (await target.GetJsonAsync<JsonElement[]>("/api/v1/gears/defaults")).Single(s => s.GetProperty("activity_type").GetInt32() == 4).GetProperty("gear_id").GetInt32());
        Assert.Single(await target.GetJsonAsync<JsonElement[]>("/api/v1/goals"));
        // An import files activities under the importer's default visibility, not the exporter's choice.
        Assert.Equal(2, imported.GetProperty("visibility").GetInt32());

        var again = await (await ImportAsync(target, bytes)).Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(0, again.GetProperty("gears").GetInt32());
        Assert.Equal(0, again.GetProperty("goals").GetInt32());
        Assert.Equal(0, again.GetProperty("health_entries").GetInt32());
        Assert.Equal(0, again.GetProperty("activities_imported").GetInt32());
        Assert.Equal(1, again.GetProperty("activities_skipped").GetInt32());
        Assert.Equal(1, (await target.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Something_that_is_not_an_export_is_refused_and_changes_nothing()
    {
        var user = await UserAsync("xfer.bad");
        var manifest = """{"format":"gravelio-export","version":1,"exported_at":"2026-01-01T00:00:00Z"}""";

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ImportAsync(user, Encoding.UTF8.GetBytes("this is not a zip"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ImportAsync(user, MakeZip(("readme.txt", "hello")))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ImportAsync(user, MakeZip(("manifest.json", """{"format":"other","version":1,"exported_at":"2026-01-01T00:00:00Z"}"""), ("data.json", "{}")))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ImportAsync(user, MakeZip(("manifest.json", manifest), ("data.json", "{ not json")))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ImportAsync(user, MakeZip(("manifest.json", manifest)), "export.txt")).StatusCode);
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task One_invalid_value_rejects_the_whole_import()
    {
        var source = await RichUserAsync("xfer.partial.source");
        var bytes = await ExportAsync(source);
        var target = await UserAsync("xfer.partial.target");

        // Same file, but the activity has a type that does not exist: the gear, goal and health rows before it must not stay behind.
        using var original = new ZipArchive(new MemoryStream(bytes));
        string Read(string name) => new StreamReader(original.GetEntry(name)!.Open()).ReadToEnd();
        var broken = Read("activities/1.json").Replace("\"activity_type\":4", "\"activity_type\":9999");
        Assert.Contains("9999", broken);
        var rejected = await ImportAsync(target, MakeZip(("manifest.json", Read("manifest.json")), ("data.json", Read("data.json")), ("activities/1.json", broken)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);

        Assert.Empty((await target.GetJsonAsync<JsonElement>("/api/v1/gears")).GetProperty("items").EnumerateArray());
        Assert.Empty(await target.GetJsonAsync<JsonElement[]>("/api/v1/goals"));
        Assert.Equal(0, (await target.GetJsonAsync<JsonElement>("/api/v1/health/weight")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task An_import_never_overwrites_the_profile_or_credentials()
    {
        var source = await RichUserAsync("xfer.profile.source");
        var bytes = await ExportAsync(source);
        var target = await UserAsync("xfer.profile.target");
        var before = await target.Http.GetStringAsync("/api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, (await ImportAsync(target, bytes)).StatusCode);
        Assert.Equal(before, await target.Http.GetStringAsync("/api/v1/users/me"));
        await api.LoginAsync("xfer.profile.target", Password); // the password still works
    }
}
