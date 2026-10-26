using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sessions;

/// Signs that device out at its next request. Ending the caller's own session is a sign-out.
public sealed class RevokeMySessionEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/users/me/sessions/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var deleted = await db.UserSessions.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct);
        if (deleted == 0) await Send.NotFoundAsync(ct);
        else await Send.NoContentAsync(ct);
    }
}
