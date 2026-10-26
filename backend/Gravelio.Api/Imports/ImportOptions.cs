using System.ComponentModel.DataAnnotations;

namespace Gravelio.Api.Imports;

public sealed class ImportOptions
{
    /// The background worker. Tests switch it off and run a pass by hand.
    public bool Enabled { get; set; } = true;

    /// Where uploaded zips wait for their job. Keep it on a volume: a job survives a restart only if its zip does.
    public string Directory
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? Path.Combine(Path.GetTempPath(), "gravelio-imports") : value.Trim();
    } = Path.Combine(Path.GetTempPath(), "gravelio-imports");

    /// Files in one zip.
    [Range(1, 100_000)]
    public int MaxFiles { get; set; } = 20_000;

    /// Bytes of activity files a zip may expand to, counted as they are read.
    [Range(1, int.MaxValue)]
    public long MaxExpandedMegabytes { get; set; } = 4096;
}
