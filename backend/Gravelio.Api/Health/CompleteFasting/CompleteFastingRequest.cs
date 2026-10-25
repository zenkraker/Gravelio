using Gravelio.Api.Data;

namespace Gravelio.Api.Health;

public sealed record CompleteFastingRequest(int Id, DateTime FastEndTime, string Status = FastingStatuses.Completed, string? Notes = null);
