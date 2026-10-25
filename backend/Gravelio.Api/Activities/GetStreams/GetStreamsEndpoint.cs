using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
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
        if (!await db.Activities.OwnedBy(User.UserId(), req.Id).AnyAsync(ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var streams = await db.ActivityStreams.AsNoTracking().Where(s => s.ActivityId == req.Id).OrderBy(s => s.StreamType).ToListAsync(ct);
        await Send.OkAsync(streams.Select(s => new StreamRead(s.StreamType, ParseWaypoints(s.Waypoints))).ToList(), ct);
    }
}
