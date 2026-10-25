using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class GetTargetsEndpoint(AppDb db) : EndpointWithoutRequest<TargetsRead>
{
    public override void Configure() => Get("/health/targets");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var target = await db.HealthTargets.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == User.UserId(), ct);
        await Send.OkAsync(TargetsRead.From(target), ct);
    }
}
