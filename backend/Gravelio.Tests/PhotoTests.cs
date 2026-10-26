using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Gravelio.Tests;

[Collection("api")]
public class PhotoTests(ApiFixture api)
{
    const string Password = "Str0ng-pass!";

    static byte[] Segment(byte marker, byte[] body) => [0xFF, marker, (byte)((body.Length + 2) >> 8), (byte)(body.Length + 2), .. body];

    /// A JPEG skeleton with a GPS-carrying EXIF block and a comment, of the given pixel size.
    static byte[] Jpeg(int width = 64, int height = 64, bool exif = true)
    {
        byte[] frame = [8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0];
        return
        [
            0xFF, 0xD8,
            .. Segment(0xE0, [.. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]),
            .. exif ? Segment(0xE1, [.. "Exif\0\0GPS 38.7223,-9.1393"u8]) : [],
            .. exif ? Segment(0xFE, "taken at the secret spot"u8.ToArray()) : [],
            .. Segment(0xDB, [0, .. new byte[64]]),
            .. Segment(0xC0, frame),
            .. Segment(0xDA, [1, 1, 0, 0, 63, 0]),
            1, 2, 3, 4, 5, 0xFF, 0xD9,
        ];
    }

    static MultipartFormDataContent Form(byte[] bytes, string name = "me.jpg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "file", name } };
    }

    [Fact]
    public void Metadata_is_stripped_and_the_picture_is_kept()
    {
        var clean = JpegPhoto.Clean(Jpeg())!;
        var text = Encoding.Latin1.GetString(clean);
        Assert.DoesNotContain("Exif", text);
        Assert.DoesNotContain("GPS", text);
        Assert.DoesNotContain("secret", text);
        Assert.Contains("JFIF", text); // the JFIF header is picture data, not metadata
        Assert.Equal(Jpeg(exif: false), clean);
    }

    [Fact]
    public void Only_a_jpeg_of_a_sane_size_is_accepted()
    {
        Assert.NotNull(JpegPhoto.Clean(Jpeg(JpegPhoto.MaxSide, JpegPhoto.MaxSide)));
        Assert.Null(JpegPhoto.Clean(Jpeg(JpegPhoto.MaxSide + 1, 10)));
        Assert.Null(JpegPhoto.Clean(Jpeg(0, 10)));
        Assert.Null(JpegPhoto.Clean("\x89PNG\r\n\x1a\n...."u8)); // a PNG
        Assert.Null(JpegPhoto.Clean("<svg onload=alert(1)></svg>"u8));
        Assert.Null(JpegPhoto.Clean([0xFF, 0xD8, 0xFF, 0xD9])); // no frame
        Assert.Null(JpegPhoto.Clean(Jpeg()[..^4])); // cut off before the end of the image
        Assert.Null(JpegPhoto.Clean(new byte[JpegPhoto.MaxBytes + 1]));
        Assert.Null(JpegPhoto.Clean([0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF])); // a segment longer than the file
    }

    [Fact]
    public async Task A_picture_can_be_set_shown_to_other_members_and_removed()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "photo.owner", Password);
        await api.CreateUserAsync(admin, "photo.viewer", Password);
        var owner = await api.LoginAsync("photo.owner", Password);
        var viewer = await api.LoginAsync("photo.viewer", Password);
        int id = (await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();
        Assert.False((await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("has_photo").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.Http.GetAsync($"/api/v1/users/{id}/photo")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.PutAsync("/api/v1/users/me/photo", Form(Jpeg()))).StatusCode);
        Assert.True((await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("has_photo").GetBoolean());

        var shown = await viewer.Http.GetAsync($"/api/v1/users/{id}/photo");
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Equal("image/jpeg", shown.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", shown.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Jpeg(exif: false), await shown.Content.ReadAsByteArrayAsync()); // what is served has no metadata
        // The people lists say who has a picture, so the SPA knows what to ask for.
        var person = await viewer.GetJsonAsync<JsonElement>($"/api/v1/people/{id}");
        Assert.True(person.GetProperty("person").GetProperty("has_photo").GetBoolean());

        // Revalidation by ETag costs no bytes.
        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{id}/photo");
        again.Headers.TryAddWithoutValidation("If-None-Match", shown.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.NotModified, (await viewer.Http.SendAsync(again)).StatusCode);

        // Only the owner changes it, and only through their own route.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.Http.DeleteAsync("/api/v1/users/me/photo")).StatusCode);
        Assert.False((await owner.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("has_photo").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.Http.GetAsync($"/api/v1/users/{id}/photo")).StatusCode);
    }

    [Fact]
    public async Task Something_that_is_not_a_picture_is_refused_and_signed_out_visitors_see_nothing()
    {
        var admin = await api.LoginAsync();
        await api.CreateUserAsync(admin, "photo.refuse", Password);
        var user = await api.LoginAsync("photo.refuse", Password);
        int id = (await user.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PutAsync("/api/v1/users/me/photo", Form(Encoding.UTF8.GetBytes("<svg onload=alert(1)>"), "me.svg"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PutAsync("/api/v1/users/me/photo", Form(Jpeg(5000, 5000)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Http.PutAsync("/api/v1/users/me/photo", Form(new byte[JpegPhoto.MaxBytes + 1]))).StatusCode);
        Assert.False((await user.GetJsonAsync<JsonElement>("/api/v1/users/me")).GetProperty("has_photo").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PutAsync("/api/v1/users/me/photo", Form(Jpeg()))).StatusCode);
        var anon = new ApiClient(api.CreateClient());
        await anon.SyncXsrfAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.Http.GetAsync($"/api/v1/users/{id}/photo")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_user_deletes_their_picture()
    {
        var admin = await api.LoginAsync();
        int id = await api.CreateUserAsync(admin, "photo.gone", Password);
        var user = await api.LoginAsync("photo.gone", Password);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Http.PutAsync("/api/v1/users/me/photo", Form(Jpeg()))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"/api/v1/users/{id}")).StatusCode);
        using var scope = api.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<AppDb>().UserPhotos.Any(p => p.UserId == id));
    }
}
