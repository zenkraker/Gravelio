using System.Globalization;
using System.IO.Compression;
using System.Text;
using Gravelio.Api.Gears;
using Microsoft.VisualBasic.FileIO;

namespace Gravelio.Api.Activities.Import;

/// Reads the activity files and their totals from a Strava "Download your data" zip.
public static class StravaExport
{
    // Strava localises the header names but keeps this column order, and repeats some names (elapsed, distance,
    // heart rate) in the legacy summary block, so columns cannot be looked up by header.
    const int NameColumn = 2, TypeColumn = 3, DescriptionColumn = 4, GearColumn = 11, FileColumn = 12;
    const int ElapsedColumn = 15, TimerColumn = 16, DistanceColumn = 17, MaxSpeedColumn = 18, AvgSpeedColumn = 19;
    const int ElevationGainColumn = 20, ElevationLossColumn = 21, MaxCadColumn = 28, AvgCadColumn = 29;
    const int MaxHrColumn = 30, AvgHrColumn = 31, MaxPowerColumn = 32, AvgPowerColumn = 33, CaloriesColumn = 34;
    const int NormalizedPowerColumn = 46;

    public sealed record StravaActivity(string File, ParsedActivity Parsed, string? Gear);

    /// A bike or pair of shoes listed in the export. Activities name one by nickname.
    public sealed record StravaGear(string Nickname, string? Brand, string? Model, int Type);

    public sealed record Result(List<StravaActivity> Activities, List<StravaGear> Gear, List<string> Errors);

    // A zip is untrusted input: cap the number and uncompressed size of entries before reading any of them.
    const int MaxEntries = 20_000;
    const long MaxEntryBytes = 100L * 1024 * 1024;
    const long MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;

    /// Parses the zip's activities.csv and the GPX/TCX/FIT file each row names. A file that fails is reported in
    /// <see cref="Result.Errors"/> instead of aborting the rest of the export.
    public static Result Read(Stream zip)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zip, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new InvalidActivityFileException("Not a zip file");
        }

        using (archive)
        {
            if (archive.Entries.Count > MaxEntries) throw new InvalidActivityFileException("The export has too many files");
            // The declared sizes only reject an honest oversized zip early; what is really read is counted by the budget.
            long uncompressed = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxEntryBytes) throw new InvalidActivityFileException($"{entry.Name} is too large");
                uncompressed += entry.Length;
                if (uncompressed > MaxUncompressedBytes) throw new InvalidActivityFileException("The export expands to too much data");
            }
            var budget = new ZipBudget(MaxUncompressedBytes, MaxEntryBytes);
            var csv = archive.Entries.FirstOrDefault(e => e.Name.Equals("activities.csv", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidActivityFileException("The zip has no activities.csv. Upload the zip Strava gives you.");
            var files = archive.Entries.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(budget.Open(csv), Encoding.UTF8);
            using var parser = new TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            parser.SetDelimiters(",");
            var header = parser.ReadFields() ?? [];
            if (header.Length <= CaloriesColumn) throw new InvalidActivityFileException("Unexpected activities.csv layout");

            var activities = new List<StravaActivity>();
            var errors = new List<string>();
            while (!parser.EndOfData)
            {
                string[] row;
                try
                {
                    row = parser.ReadFields() ?? [];
                }
                catch (MalformedLineException)
                {
                    errors.Add($"activities.csv line {parser.LineNumber}: unreadable row");
                    continue;
                }
                var file = Text(row, FileColumn);
                if (file is null) continue; // No recording file (manual entry): nothing to show on the map.
                if (!files.TryGetValue(Path.GetFileName(file), out var entry))
                {
                    errors.Add($"{file}: missing from the zip");
                    continue;
                }
                try
                {
                    activities.Add(new StravaActivity(file, ReadRow(row, entry, budget), Text(row, GearColumn)));
                }
                catch (InvalidActivityFileException e)
                {
                    errors.Add($"{file}: {e.Message}");
                }
            }
            if (activities.Count == 0 && errors.Count == 0) throw new InvalidActivityFileException("activities.csv lists no activities");
            // Strava only exports bikes and shoes; a zip built from part of the export may have neither file.
            var gear = ReadGear(archive, "bikes.csv", GearTypes.Bike, budget)
                .Concat(ReadGear(archive, "shoes.csv", GearTypes.Shoes, budget))
                .DistinctBy(g => g.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new Result(activities, gear, errors);
        }
    }

    static List<StravaGear> ReadGear(ZipArchive archive, string fileName, int type, ZipBudget budget)
    {
        var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return [];
        using var reader = new StreamReader(budget.Open(entry), Encoding.UTF8);
        using var parser = new TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        parser.ReadFields(); // Localised header; the column order is stable.
        var gear = new List<StravaGear>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields() ?? [];
            if (Text(row, 0) is not { } nickname) continue;
            gear.Add(new StravaGear(nickname, Text(row, 1), Text(row, 2), type));
        }
        return gear;
    }

    static ParsedActivity ReadRow(string[] row, ZipArchiveEntry entry, ZipBudget budget)
    {
        using var raw = budget.Open(entry);
        string name = entry.Name;
        Stream stream = raw;
        GZipStream? gzip = null;
        try
        {
            // Strava gzips the older recordings.
            if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                gzip = new GZipStream(raw, CompressionMode.Decompress);
                stream = gzip;
                name = Path.GetFileNameWithoutExtension(name);
            }
            var parsed = ActivityFileParser.Parse(stream, Path.GetExtension(name)).FirstOrDefault()
                ?? throw new InvalidActivityFileException("No activity in file");

            // The GPX export carries no HR, cadence or power, so the CSV totals are the only source for those.
            var start = parsed.Summary?.StartTime ?? parsed.Points.FirstOrDefault()?.Time
                ?? throw new InvalidActivityFileException("No timed points in file");
            var elapsed = Num(row, ElapsedColumn)
                ?? (parsed.Points.Count > 1 ? (parsed.Points[^1].Time - start).TotalSeconds : null);
            if (elapsed is not > 0) throw new InvalidActivityFileException("No elapsed time in activities.csv");

            parsed.Name = Text(row, NameColumn) ?? parsed.Name;
            parsed.Description = Text(row, DescriptionColumn) ?? parsed.Description;
            // Old recordings (e.g. Runtastic) carry no <type>. The CSV column is localised, so translate the
            // Italian labels Strava writes for this profile; English labels already match ActivityTypes.
            parsed.Sport ??= Text(row, TypeColumn) switch
            {
                "Ciclismo" => "cycling",
                "Pedalata con e-bike" => "e_bike_ride",
                var localized => localized,
            };
            parsed.Summary = new DeviceSummary(start, elapsed.Value, Num(row, TimerColumn), Num(row, DistanceColumn),
                Int(row, CaloriesColumn), Int(row, AvgHrColumn), Int(row, MaxHrColumn), Int(row, AvgCadColumn), Int(row, MaxCadColumn),
                Int(row, AvgPowerColumn), Int(row, MaxPowerColumn), Int(row, NormalizedPowerColumn),
                Int(row, ElevationGainColumn), Int(row, ElevationLossColumn), Num(row, AvgSpeedColumn), Num(row, MaxSpeedColumn));
            return parsed;
        }
        finally
        {
            gzip?.Dispose();
        }
    }

    static string? Text(string[] row, int column) =>
        column < row.Length && !string.IsNullOrWhiteSpace(row[column]) ? row[column].Trim() : null;

    static double? Num(string[] row, int column) =>
        double.TryParse(Text(row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;

    // A zero average heart rate or power is a missing value, not a measurement.
    static int? Int(string[] row, int column) => Num(row, column) is { } v && v > 0 ? (int)v : null;
}
