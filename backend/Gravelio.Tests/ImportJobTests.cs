using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gravelio.Api.Data;
using Gravelio.Api.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

[Collection("api")]
public class ImportJobTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    /// A zip with the given GPX activities, a broken file, and entries that must be ignored.
    static byte[] Zip(DateTime firstDay, int activities, bool withBroken = true, int paddingBytes = 0)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            for (int i = 0; i < activities; i++)
                Add($"rides/day{i}.gpx", TestData.Gpx($"Ride {i}", firstDay.AddDays(i)) + (paddingBytes > 0 && i == 0 ? "<!--" + new string('x', paddingBytes) + "-->" : ""));
            if (withBroken) Add("rides/broken.gpx", "this is not xml");
            Add("readme.txt", "not an activity");
            Add("__MACOSX/rides/._day0.gpx", "resource fork");
            Add("rides/.hidden.gpx", TestData.Gpx("Hidden file", firstDay.AddDays(400)));
        }
        return ms.ToArray();
    }

    async Task<ApiClient> UserAsync(string username)
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, username, Password);
        return await api.LoginAsync(username, Password);
    }

    static Task<HttpResponseMessage> StartAsync(ApiClient user, byte[] zip, string? key = null, string name = "history.zip")
    {
        var file = new ByteArrayContent(zip);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/activities/import/zip") { Content = new MultipartFormDataContent { { file, "file", name } } };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return user.Http.SendAsync(request);
    }

    static async Task<JsonElement> JobAsync(ApiClient user, int id) => await user.GetJsonAsync<JsonElement>($"/api/v1/activities/import/jobs/{id}");

    static async Task<int> IdOf(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json)).GetProperty("id").GetInt32();

    Task RunJobAsync(Microsoft.Extensions.DependencyInjection.IServiceScope? _ = null) => api.Services.GetRequiredService<BulkImportJob>().RunOnceAsync(CancellationToken.None);

    [Fact]
    public async Task A_zip_of_activities_is_imported_in_the_background_with_progress_and_failures()
    {
        var user = await UserAsync("bulk.owner");
        var start = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var started = await StartAsync(user, Zip(start, 3));
        Assert.True(started.StatusCode == HttpStatusCode.Accepted, await started.Content.ReadAsStringAsync());
        int id = await IdOf(started);

        var queued = await JobAsync(user, id);
        Assert.Equal("queued", queued.GetProperty("status").GetString());
        Assert.Equal(4, queued.GetProperty("total_files").GetInt32()); // 3 rides and the broken one; the readme and the macOS and dot files are not counted
        Assert.Equal(0, queued.GetProperty("processed_files").GetInt32());

        await RunJobAsync();
        var done = await JobAsync(user, id);
        Assert.Equal("completed", done.GetProperty("status").GetString());
        Assert.Equal(4, done.GetProperty("processed_files").GetInt32());
        Assert.Equal(3, done.GetProperty("imported").GetInt32());
        Assert.Equal(1, done.GetProperty("failed").GetInt32());
        var failure = Assert.Single(done.GetProperty("failures").EnumerateArray());
        Assert.Equal("rides/broken.gpx", failure.GetProperty("file").GetString());
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("finished_at").ValueKind);
        Assert.Equal(3, (await user.GetJsonAsync<JsonElement>("/api/v1/activities?page_size=50")).GetProperty("total").GetInt32());
        Assert.Empty(Directory.GetFiles(api.ImportDirectory, "*.zip")); // the zip is gone once the job is done

        // The same history again changes nothing: every ride is already there.
        int again = await IdOf(await StartAsync(user, Zip(start, 3)));
        await RunJobAsync();
        var repeat = await JobAsync(user, again);
        Assert.Equal(0, repeat.GetProperty("imported").GetInt32());
        Assert.Equal(3, repeat.GetProperty("skipped").GetInt32());
        Assert.Equal(3, (await user.GetJsonAsync<JsonElement>("/api/v1/activities?page_size=50")).GetProperty("total").GetInt32());

        var listed = (await user.GetJsonAsync<JsonElement>("/api/v1/activities/import/jobs")).GetProperty("items").EnumerateArray().Select(j => j.GetProperty("id").GetInt32());
        Assert.Equal([again, id], listed); // newest first
    }

    [Fact]
    public async Task One_import_at_a_time_and_a_retried_request_finds_its_job()
    {
        var user = await UserAsync("bulk.once");
        var zip = Zip(new DateTime(2024, 2, 1, 8, 0, 0, DateTimeKind.Utc), 1);
        var first = await StartAsync(user, zip, key: "abc-123");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        int id = await IdOf(first);

        var retry = await StartAsync(user, zip, key: "abc-123"); // the client never saw the answer and tried again
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(id, await IdOf(retry));

        var second = await StartAsync(user, zip);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        await RunJobAsync();
        Assert.Equal("completed", (await JobAsync(user, id)).GetProperty("status").GetString());
        // Once it is done, the next one may start; the key still names the first.
        Assert.Equal(HttpStatusCode.Accepted, (await StartAsync(user, Zip(new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc), 1))).StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_queued_import_removes_its_zip_and_frees_the_slot()
    {
        var user = await UserAsync("bulk.cancel");
        int id = await IdOf(await StartAsync(user, Zip(new DateTime(2024, 4, 1, 8, 0, 0, DateTimeKind.Utc), 2)));
        Assert.True(File.Exists(Path.Combine(api.ImportDirectory, $"{id}.zip")));

        var cancelled = await user.Http.PostAsync($"/api/v1/activities/import/jobs/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("cancelled", (await JobAsync(user, id)).GetProperty("status").GetString());
        Assert.False(File.Exists(Path.Combine(api.ImportDirectory, $"{id}.zip")));
        Assert.Equal(HttpStatusCode.Conflict, (await user.Http.PostAsync($"/api/v1/activities/import/jobs/{id}/cancel", null)).StatusCode);

        await RunJobAsync(); // nothing left to do
        Assert.Equal(0, (await user.GetJsonAsync<JsonElement>("/api/v1/activities")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Accepted, (await StartAsync(user, Zip(new DateTime(2024, 4, 1, 8, 0, 0, DateTimeKind.Utc), 2))).StatusCode);
    }

    [Fact]
    public async Task Nobody_sees_or_cancels_another_persons_import_and_what_is_not_a_zip_is_refused()
    {
        var owner = await UserAsync("bulk.private");
        var other = await UserAsync("bulk.nosy");
        int id = await IdOf(await StartAsync(owner, Zip(new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Utc), 1)));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync($"/api/v1/activities/import/jobs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.PostAsync($"/api/v1/activities/import/jobs/{id}/cancel", null)).StatusCode);
        Assert.Empty((await other.GetJsonAsync<JsonElement>("/api/v1/activities/import/jobs")).GetProperty("items").EnumerateArray());
        await RunJobAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await StartAsync(other, Encoding.UTF8.GetBytes("definitely not a zip"))).StatusCode);
        using var empty = new MemoryStream();
        using (var zip = new ZipArchive(empty, ZipArchiveMode.Create, leaveOpen: true)) zip.CreateEntry("notes.txt");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await StartAsync(other, empty.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(other, Zip(DateTime.UtcNow, 1), name: "history.rar")).StatusCode);
        Assert.Empty(Directory.GetFiles(api.ImportDirectory, "*.upload")); // refused uploads leave nothing behind
    }

    [Fact]
    public async Task A_zip_that_expands_past_the_limit_stops_the_import_whatever_its_headers_say()
    {
        var user = await UserAsync("bulk.bomb");
        using var factory = api.WithWebHostBuilder(b => b.UseSetting("ImportJobs:MaxExpandedMegabytes", "1"));
        // Two megabytes of padding compress to almost nothing, so the upload is small and the limit has to count what is really read.
        var zip = Zip(new DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc), 2, withBroken: false, paddingBytes: 2 * 1024 * 1024);
        Assert.True(zip.Length < 100_000);
        int id = await IdOf(await StartAsync(user, zip));

        await factory.Services.GetRequiredService<BulkImportJob>().RunOnceAsync(CancellationToken.None);
        var job = await JobAsync(user, id);
        Assert.Equal("failed", job.GetProperty("status").GetString());
        Assert.Contains("more data", job.GetProperty("error").GetString());
        Assert.Equal(0, job.GetProperty("imported").GetInt32());
    }

    [Fact]
    public async Task A_job_that_stopped_being_heard_from_is_queued_again_and_old_orphans_are_swept()
    {
        var user = await UserAsync("bulk.crash");
        int id = await IdOf(await StartAsync(user, Zip(new DateTime(2024, 7, 1, 8, 0, 0, DateTimeKind.Utc), 2, withBroken: false)));
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDb>().ImportJobs.Where(j => j.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "running").SetProperty(j => j.HeartbeatAt, DateTime.UtcNow.AddMinutes(-10)));

        var oldOrphan = Path.Combine(api.ImportDirectory, "999999.zip");
        var freshOrphan = Path.Combine(api.ImportDirectory, "999998.zip");
        await File.WriteAllTextAsync(oldOrphan, "x");
        await File.WriteAllTextAsync(freshOrphan, "x");
        File.SetLastWriteTimeUtc(oldOrphan, DateTime.UtcNow.AddHours(-3));

        await RunJobAsync();
        var job = await JobAsync(user, id);
        Assert.Equal("completed", job.GetProperty("status").GetString());
        Assert.Equal(2, job.GetProperty("imported").GetInt32());
        Assert.False(File.Exists(oldOrphan));
        Assert.True(File.Exists(freshOrphan)); // it may belong to an upload still in progress
        File.Delete(freshOrphan);
    }
}
