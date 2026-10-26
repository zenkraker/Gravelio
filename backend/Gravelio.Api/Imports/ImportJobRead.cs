using System.Text.Json;
using Gravelio.Api.Data;

namespace Gravelio.Api.Imports;

public sealed record ImportFailure(string File, string Error);

public sealed record ImportJobRead(int Id, string Status, string FileName, int TotalFiles, int ProcessedFiles, int Imported, int Skipped, int HiddenDuplicates,
    int Failed, List<ImportFailure> Failures, string? Error, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt)
{
    /// A read with the failure list filled in from its stored JSON (not translatable by the database).
    public static ImportJobRead From(ImportJob j) => new(j.Id, j.Status, j.FileName, j.TotalFiles, j.ProcessedFiles, j.Imported, j.Skipped, j.HiddenDuplicates,
        j.Failed, j.Failures is null ? [] : JsonSerializer.Deserialize<List<ImportFailure>>(j.Failures, Json) ?? [], j.Error, j.CreatedAt, j.StartedAt, j.FinishedAt);

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
}
