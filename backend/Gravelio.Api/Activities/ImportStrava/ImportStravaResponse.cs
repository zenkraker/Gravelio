namespace Gravelio.Api.Activities;

/// <param name="Imported">Activities saved.</param>
/// <param name="Skipped">Activities already present (or repeated in the export).</param>
/// <param name="Failed">Files that could not be read; the first few reasons are in <paramref name="Errors"/>.</param>
public sealed record ImportStravaResponse(int Imported, int Skipped, int Failed, List<string> Errors);
