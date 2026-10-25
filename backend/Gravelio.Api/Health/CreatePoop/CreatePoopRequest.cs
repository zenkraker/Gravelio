namespace Gravelio.Api.Health;

public sealed record CreatePoopRequest(DateTime DateTime, int? BristolType = null, string? Color = null, string? Notes = null);
