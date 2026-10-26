using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sessions;

public sealed class ListMySessionsRequest : PagedRequest;

public sealed class ListMySessionsValidator : PagedValidator<ListMySessionsRequest>;

public sealed class ListMySessionsEndpoint(AppDb db, TimeProvider time) : Endpoint<ListMySessionsRequest, PagedResult<SessionRead>>
{
    public override void Configure() => Get("/users/me/sessions");

    public override async Task HandleAsync(ListMySessionsRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var now = time.GetUtcNow().UtcDateTime;
        var current = SessionRead.CurrentId(User);
        await Send.OkAsync(await db.UserSessions.AsNoTracking().Where(s => s.UserId == me && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt).ThenByDescending(s => s.Id)
            .ToPageAsync(req, page => SessionRead.Project(page, current), ct), ct);
    }
}
