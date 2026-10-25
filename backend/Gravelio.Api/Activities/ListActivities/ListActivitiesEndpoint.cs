using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ListActivitiesEndpoint(AppDb db) : Endpoint<ListActivitiesRequest, PagedResult<ActivityRead>>
{
    public override void Configure() => Get("/activities");

    public override async Task HandleAsync(ListActivitiesRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        await Send.OkAsync(await db.Activities.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.StartTime)
            .ToPageAsync(req, page => page.Select(ActivityRead.Projection), ct), ct);
    }
}
