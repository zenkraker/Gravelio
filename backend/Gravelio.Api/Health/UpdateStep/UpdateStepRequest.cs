namespace Gravelio.Api.Health;

public sealed record UpdateStepRequest(int Id, DateOnly Date, int Steps);
