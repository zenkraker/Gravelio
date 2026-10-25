using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Followers;

public sealed class ListPeopleEndpoint(AppDb db) : Endpoint<PeopleListRequest, PagedResult<PersonRead>>
{
    public override void Configure() => Get("/people");

    public override async Task HandleAsync(PeopleListRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var query = db.Users.AsNoTracking().Where(u => u.Active && u.Id != me);
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var pattern = QueryExtensions.ContainsPattern(req.Q.Trim());
            query = query.Where(u => EF.Functions.ILike(u.Name, pattern, "\\") || EF.Functions.ILike(u.UserName!, pattern, "\\"));
        }
        await Send.OkAsync(await query.OrderBy(u => u.Name).ThenBy(u => u.Id)
            .ToPageAsync(req, page => page.Select(PersonRead.Projection), ct), ct);
    }
}
