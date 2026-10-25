using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace Gravelio.Tests;

static class TestData
{
    /// A 60-point track 10 s apart heading NNE from central Lisbon: 3,618 m long and 590 s elapsed.
    public static string Gpx(string name, DateTime start, int points = 60, string sport = "running")
    {
        // Invariant culture: a comma decimal separator would produce coordinates the parser rejects.
        var pts = string.Concat(Enumerable.Range(0, points).Select(i => string.Create(CultureInfo.InvariantCulture,
            $"<trkpt lat=\"{38.7223 + i * 0.0005}\" lon=\"{-9.1393 + i * 0.0003}\"><ele>{100 + i}</ele><time>{start.AddSeconds(i * 10):yyyy-MM-ddTHH:mm:ssZ}</time></trkpt>")));
        return $"<gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><trk><name>{name}</name><type>{sport}</type><trkseg>{pts}</trkseg></trk></gpx>";
    }

    public static MultipartFormDataContent Upload(string fileName, string content)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
