using System.Security.Cryptography;

namespace Gravelio.Api.Activities;

public static class ActivityDuplicates
{
    /// A hash of the file's bytes, read from the stream's start; the stream is rewound for the parser.
    public static async Task<string> HashAsync(Stream file, CancellationToken ct)
    {
        file.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
        file.Position = 0;
        return hash;
    }

    /// One recording is one file and one start: a multi-activity file gives each of its activities its own key.
    public static string Key(string fileHash, DateTime startUtc) => $"{fileHash}|{startUtc:O}";
}
