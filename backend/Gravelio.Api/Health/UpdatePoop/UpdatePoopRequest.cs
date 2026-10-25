namespace Gravelio.Api.Health;

public sealed record UpdatePoopRequest(int Id, DateTime DateTime, int? BristolType = null, string? Color = null, string? Notes = null);
