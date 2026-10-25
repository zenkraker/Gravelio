namespace Gravelio.Api.Health;

public sealed record CreateFastingRequest(DateTime FastStartTime, DateTime? FastEndTime = null, int? TargetDurationSeconds = null,
    string? FastingType = null, string? Notes = null);
