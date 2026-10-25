using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Gears;

public sealed class ListComponentTypesEndpoint : EndpointWithoutRequest<Dictionary<int, string[]>>
{
    public override void Configure() => Get("/gears/component-types");

    public override Task HandleAsync(CancellationToken ct) =>
        Send.OkAsync(Enumerable.Range(GearTypes.Bike, GearTypes.WaterSportsBoard).ToDictionary(t => t, t => GearTypes.ComponentTypesFor(t).Order().ToArray()), ct);
}
