using System.IO.Compression;
using Gravelio.Api.Activities.Import;

namespace Gravelio.Api.Imports;

/// Which entries of an uploaded zip are activity files, and where a job's zip is kept.
public static class ImportFiles
{
    public static bool IsActivityFile(ZipArchiveEntry entry) =>
        entry.Name.Length > 0 && !entry.Name.StartsWith('.') && !entry.FullName.StartsWith("__MACOSX/", StringComparison.Ordinal)
        && ActivityFileParser.SupportedExtensions.Contains(Path.GetExtension(entry.Name).ToLowerInvariant());

    public static string PathFor(ImportOptions options, int jobId) => Path.Combine(options.Directory, $"{jobId}.zip");

    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The sweep removes what is left.
        }
    }
}
