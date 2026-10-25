using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class ListFollowRequestsEndpoint(AppDb db) : Endpoint<ListFollowRequestsRequest, PagedResult<FollowRead>>
{
    public override void Configure() => Get("/follow-requests");

    public override async Task HandleAsync(ListFollowRequestsRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        await Send.OkAsync(await db.Follows.AsNoTracking()
            .Where(f => f.FolloweeId == me && f.Status == FollowStatuses.Pending)
            .OrderBy(f => f.Id)
            .ToPageAsync(req, page => page.ToRead(), ct), ct);
    }
}
