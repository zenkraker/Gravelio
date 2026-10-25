using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListGearsRequest : PagedRequest
{
    /// Nickname contains this text, ignoring case.
    public string? Q { get; init; }

    [BindFrom("gear_type")]
    public int? GearType { get; init; }

    public bool? Active { get; init; }
}
