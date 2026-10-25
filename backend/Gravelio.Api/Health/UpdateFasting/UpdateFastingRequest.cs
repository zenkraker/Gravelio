using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record UpdateFastingRequest(int Id, DateTime FastStartTime, DateTime? FastEndTime = null, int? TargetDurationSeconds = null,
    int? ActualDurationSeconds = null, string? FastingType = null, string Status = FastingStatuses.InProgress, string? Notes = null);
