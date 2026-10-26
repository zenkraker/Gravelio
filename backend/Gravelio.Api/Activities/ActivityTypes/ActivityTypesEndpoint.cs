using FastEndpoints;

namespace Gravelio.Api.Activities;

public sealed class ActivityTypesEndpoint : EndpointWithoutRequest<IReadOnlyDictionary<int, string>>
{
    // Static reference data (names only), and the public share page needs it too.
    public override void Configure()
    {
        Get("/activities/types");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(ActivityTypes.Names, ct);
}
