using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sessions;

public sealed class RevokeUserSessionRequest
{
    public int UserId { get; init; }
    public int SessionId { get; init; }
}

/// An admin ends one device's session for any user.
public sealed class RevokeUserSessionEndpoint(AppDb db) : Endpoint<RevokeUserSessionRequest>
{
    public override void Configure()
    {
        Delete("{userId:int}/sessions/{sessionId:int}");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(RevokeUserSessionRequest req, CancellationToken ct)
    {
        var deleted = await db.UserSessions.OwnedBy(req.UserId, req.SessionId).ExecuteDeleteAsync(ct);
        if (deleted == 0) await Send.NotFoundAsync(ct);
        else await Send.NoContentAsync(ct);
    }
}
