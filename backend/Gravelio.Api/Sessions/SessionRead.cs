using System.Security.Claims;
using Gravelio.Api.Data;

namespace Gravelio.Api.Sessions;

public sealed record SessionRead(int Id, string Device, string? IpAddress, DateTime CreatedAt, DateTime LastSeenAt, bool Current)
{
    /// Id of the caller's own session, or 0 when the caller has none (an API key, which never lists sessions anyway).
    public static int CurrentId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(SessionTicketStore.SessionIdClaim), out var id) ? id : 0;

    public static IQueryable<SessionRead> Project(IQueryable<UserSession> sessions, int currentId) =>
        sessions.Select(s => new SessionRead(s.Id, s.Device, s.IpAddress, s.CreatedAt, s.LastSeenAt, s.Id == currentId));
}
