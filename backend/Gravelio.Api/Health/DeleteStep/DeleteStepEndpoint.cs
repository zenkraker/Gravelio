using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Health;

public sealed class DeleteStepEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Delete("/health/steps/{id:int}");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        if (await db.HealthSteps.OwnedBy(User.UserId(), req.Id).ExecuteDeleteAsync(ct) > 0) await Send.NoContentAsync(ct);
        else await Send.NotFoundAsync(ct);
    }
}
