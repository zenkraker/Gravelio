namespace Gravelio.Api.Health;

public static class HealthTime
{
    /// Npgsql's timestamptz needs an explicit UTC kind; a datetime-local from the SPA arrives unspecified.
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? Utc(DateTime? value) => value is null ? null : Utc(value.Value);
}
