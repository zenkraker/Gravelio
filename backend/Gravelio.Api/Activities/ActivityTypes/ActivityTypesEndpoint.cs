using System.Text.Json;
using FastEndpoints;
using Gravelio.Api.Activities.Import;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Activities;

public sealed class ActivityTypesEndpoint : EndpointWithoutRequest<IReadOnlyDictionary<int, string>>
{
    public override void Configure() => Get("/activities/types");

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(ActivityTypes.Names, ct);
}
