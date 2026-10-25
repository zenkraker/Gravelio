namespace Gravelio.Api.Health;

public sealed record UpdateWaterRequest(int Id, DateOnly Date, decimal AmountMl);
