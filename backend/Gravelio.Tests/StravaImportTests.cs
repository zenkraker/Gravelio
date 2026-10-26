using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static Gravelio.Tests.TestData;

namespace Gravelio.Tests;

[Collection("api")]
public class StravaImportTests(ApiFixture api)
{
    static string CsvRow(params (int Column, string Value)[] cells)
    {
        var row = new string[35];
        foreach (var (column, value) in cells) row[column] = value;
        return string.Join(",", row);
    }

    static byte[] Zip(params (string Path, string Content)[] files)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }
        return buffer.ToArray();
    }

    static MultipartFormDataContent UploadZip(byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        return new MultipartFormDataContent { { file, "file", "export.zip" } };
    }

    [Fact]
    public async Task Strava_export_imports_activities_and_skips_a_repeat()
    {
        var admin = await api.LoginAsync();
        var start = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        const string bike = "strava-test-bike";
        var row = CsvRow((0, "1"), (2, "Strava import"), (11, bike), (12, "activities/1.gpx"), (15, "590"), (17, "3620"),
            (20, "42"), (30, "170"), (31, "140"), (33, "200"), (34, "250"));
        var csv = CsvRow((0, "Activity ID")) + "\n" + row + "\n";
        var bikes = "Nome della bicicletta,Marca della bicicletta,Modello della bicicletta,Sport predefiniti per la bici\n" +
            $"{bike},Scott,Speedster gravel 30 eq,Gravel biking\n";
        var zip = Zip(("activities.csv", csv), ("activities/1.gpx", Gpx("Strava import", start, sport: "gravel_biking")),
            ("bikes.csv", bikes));

        var response = await admin.Http.PostAsync("/api/v1/activities/import/strava", UploadZip(zip));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(1, result.GetProperty("imported").GetInt32());
        Assert.Equal(0, result.GetProperty("skipped").GetInt32());
        Assert.Equal(0, result.GetProperty("failed").GetInt32());

        var list = await admin.GetJsonAsync<JsonElement>("/api/v1/activities?page=1&page_size=100");
        var activity = list.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("name").GetString() == "Strava import");
        Assert.Equal(3620, activity.GetProperty("distance").GetInt32()); // the CSV total, not the GPX sum
        Assert.Equal(5, activity.GetProperty("activity_type").GetInt32());
        Assert.Equal(170, activity.GetProperty("max_hr").GetInt32());
        Assert.Equal(250, activity.GetProperty("calories").GetInt32());

        var gears = await admin.GetJsonAsync<JsonElement>($"/api/v1/gears?q={bike}&page=1&page_size=100");
        var gear = gears.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Scott", gear.GetProperty("brand").GetString());
        Assert.Equal("Speedster gravel 30 eq", gear.GetProperty("model").GetString());
        Assert.Equal(1, gear.GetProperty("gear_type").GetInt32());
        Assert.Equal(gear.GetProperty("id").GetInt32(), activity.GetProperty("gear_id").GetInt32());

        var repeatResponse = await admin.Http.PostAsync("/api/v1/activities/import/strava", UploadZip(zip));
        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var repeat = await repeatResponse.Content.ReadFromJsonAsync<JsonElement>(ApiClient.Json);
        Assert.Equal(0, repeat.GetProperty("imported").GetInt32());
        Assert.Equal(1, repeat.GetProperty("skipped").GetInt32());
        // The gear is reused, not duplicated, on a second import.
        Assert.Equal(1, (await admin.GetJsonAsync<JsonElement>($"/api/v1/gears?q={bike}&page=1&page_size=100")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Not_a_zip_is_a_422()
    {
        var admin = await api.LoginAsync();
        var response = await admin.Http.PostAsync("/api/v1/activities/import/strava", Upload("export.zip", "not a zip"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
