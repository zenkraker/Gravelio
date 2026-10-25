using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class ActiveFastingEndpoint(AppDb db) : EndpointWithoutRequest<FastingRead>
{
    public override void Configure() => Get("/health/fasting/active");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fast = await db.HealthFastings.AsNoTracking()
            .Where(f => f.UserId == User.UserId() && f.Status == FastingStatuses.InProgress)
            .Select(FastingRead.Projection).FirstOrDefaultAsync(ct);
        if (fast is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(fast, ct);
    }
}
