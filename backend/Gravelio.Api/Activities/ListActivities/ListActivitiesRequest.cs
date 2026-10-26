using FastEndpoints;

namespace Gravelio.Api.Activities;

public sealed class ListActivitiesRequest : PagedRequest
{
    /// An activity type id from <c>GET /activities/types</c>.
    public int? Type { get; init; }

    /// First and last local calendar day, inclusive: the day an activity was recorded on, in the zone it was recorded in.
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// The name contains this text, ignoring case.
    public string? Q { get; init; }

    [BindFrom("gear_id")]
    public int? GearId { get; init; }

    /// start_time (default), name, type, distance, duration, elevation, calories or average_hr.
    public string Sort { get; init; } = "start_time";

    /// asc or desc (default).
    public string Order { get; init; } = "desc";
}
