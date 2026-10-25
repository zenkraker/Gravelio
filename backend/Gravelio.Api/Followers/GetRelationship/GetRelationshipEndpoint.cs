using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class GetRelationshipEndpoint(AppDb db) : Endpoint<IdRequest, RelationshipRead>
{
    public override void Configure() => Get("/users/{id:int}/relationship");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        if (!await db.Users.AnyAsync(u => u.Id == req.Id, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var outgoing = await db.Follows.AsNoTracking().Where(f => f.FollowerId == me && f.FolloweeId == req.Id)
            .ToRead().FirstOrDefaultAsync(ct);
        var incoming = await db.Follows.AsNoTracking().Where(f => f.FollowerId == req.Id && f.FolloweeId == me)
            .ToRead().FirstOrDefaultAsync(ct);
        await Send.OkAsync(new RelationshipRead(outgoing, incoming), ct);
    }
}
