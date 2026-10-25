using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class GetStreamsEndpoint(AppDb db) : Endpoint<IdRequest, List<StreamRead>>
{
    // Clone lets the JsonDocument (and its pooled buffers) be released right away.
    static JsonElement ParseWaypoints(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public override void Configure() => Get("/activities/{id:int}/streams");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var me = User.UserId();
        var activity = await db.Activities.AsNoTracking().VisibleTo(db, me).Where(a => a.Id == req.Id)
            .Select(a => new { a.UserId, a.StartTime }).FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var isOwner = activity.UserId == me;
        var settings = isOwner ? null : await PrivacyMask.ForOwnerAsync(db, activity.UserId, ct);
        var hideStart = !isOwner && settings is { HideActivityStartTime: true };
        var streams = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == req.Id).OrderBy(s => s.StreamType).ToListAsync(ct);
        var visible = streams.Where(s => PrivacyMask.IsStreamVisible(s.StreamType, settings, isOwner))
            .Select(s => new StreamRead(s.StreamType, hideStart ? PrivacyMask.RelativeWaypoints(s.Waypoints, activity.StartTime) : ParseWaypoints(s.Waypoints)))
            .ToList();
        await Send.OkAsync(visible, ct);
    }
}
