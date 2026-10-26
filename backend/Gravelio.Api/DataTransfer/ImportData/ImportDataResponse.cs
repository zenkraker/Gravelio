namespace Gravelio.Api.DataTransfer;

/// What an import did. Anything already in the account is skipped, so importing the same file again changes nothing.
public sealed record ImportDataResponse(int Gears, int Goals, int HealthEntries, int ActivitiesImported, int ActivitiesSkipped);
