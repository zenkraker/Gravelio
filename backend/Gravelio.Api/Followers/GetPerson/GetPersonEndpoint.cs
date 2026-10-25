using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class GetPersonEndpoint(AppDb db) : Endpoint<IdRequest, PersonProfileRead>
{
    public override void Configure() => Get("/people/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var person = await db.Users.AsNoTracking().Where(u => u.Id == req.Id && u.Active)
            .Select(PersonRead.Projection).FirstOrDefaultAsync(ct);
        if (person is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var followers = await db.Follows.CountAsync(f => f.FolloweeId == req.Id && f.Status == FollowStatuses.Accepted, ct);
        var following = await db.Follows.CountAsync(f => f.FollowerId == req.Id && f.Status == FollowStatuses.Accepted, ct);
        var outgoing = await db.Follows.AsNoTracking().Where(f => f.FollowerId == me && f.FolloweeId == req.Id)
            .ToRead().FirstOrDefaultAsync(ct);
        var incoming = await db.Follows.AsNoTracking().Where(f => f.FollowerId == req.Id && f.FolloweeId == me)
            .ToRead().FirstOrDefaultAsync(ct);
        await Send.OkAsync(new PersonProfileRead(person, followers, following, outgoing, incoming), ct);
    }
}
