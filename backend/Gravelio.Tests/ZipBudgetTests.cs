using System.IO.Compression;
using Gravelio.Api.Activities.Import;

namespace Gravelio.Tests;

public class ZipBudgetTests
{
    static ZipArchiveEntry Entry(ZipArchive zip) => zip.Entries[0];

    static MemoryStream Zip(int bytes)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = zip.CreateEntry("a.csv").Open();
            s.Write(new byte[bytes]);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Reading_past_the_entry_limit_is_refused_whatever_the_header_says()
    {
        using var zip = new ZipArchive(Zip(5000), ZipArchiveMode.Read);
        using var s = new ZipBudget(1_000_000, 1000).Open(Entry(zip));
        Assert.Throws<InvalidActivityFileException>(() => s.CopyTo(Stream.Null));
    }

    [Fact]
    public void Reading_past_the_shared_total_is_refused_and_within_limits_is_fine()
    {
        using var zip = new ZipArchive(Zip(800), ZipArchiveMode.Read);
        var budget = new ZipBudget(1000, 1000);
        using (var s = budget.Open(Entry(zip))) s.CopyTo(Stream.Null);
        using var again = budget.Open(Entry(zip));
        Assert.Throws<InvalidActivityFileException>(() => again.CopyTo(Stream.Null));
    }
}
