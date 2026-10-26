using FastEndpoints;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Sessions;

public sealed class ListUserSessionsRequest : PagedRequest
{
    public int Id { get; init; }
}

public sealed class ListUserSessionsValidator : PagedValidator<ListUserSessionsRequest>;

public sealed class ListUserSessionsEndpoint(AppDb db, TimeProvider time) : Endpoint<ListUserSessionsRequest, PagedResult<SessionRead>>
{
    public override void Configure()
    {
        Get("{id:int}/sessions");
        Group<AdminUsersGroup>();
    }

    public override async Task HandleAsync(ListUserSessionsRequest req, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var current = SessionRead.CurrentId(User);
        await Send.OkAsync(await db.UserSessions.AsNoTracking().Where(s => s.UserId == req.Id && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt).ThenByDescending(s => s.Id)
            .ToPageAsync(req, page => SessionRead.Project(page, current), ct), ct);
    }
}
