using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListGearsEndpoint(AppDb db) : Endpoint<ListGearsRequest, PagedResult<GearRead>>
{
    public override void Configure() => Get("/gears");

    public override async Task HandleAsync(ListGearsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var query = db.Gears.AsNoTracking().Where(g => g.UserId == userId);
        if (!string.IsNullOrWhiteSpace(req.Q)) query = query.Where(g => EF.Functions.ILike(g.Nickname, GearQueries.ContainsPattern(req.Q.Trim()), "\\"));
        if (req.GearType is { } type) query = query.Where(g => g.GearType == type);
        if (req.Active is { } active) query = query.Where(g => g.Active == active);

        await Send.OkAsync(await query.OrderBy(g => g.Nickname).ThenBy(g => g.Id).ToPageAsync(req, page => page.ToRead(db), ct), ct);
    }
}
