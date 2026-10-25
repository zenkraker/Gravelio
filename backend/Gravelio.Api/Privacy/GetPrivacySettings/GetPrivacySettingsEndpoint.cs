using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Privacy;

public sealed class GetPrivacySettingsEndpoint(AppDb db) : EndpointWithoutRequest<PrivacyRead>
{
    public override void Configure() => Get("/users/me/privacy");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == User.UserId(), ct);
        await Send.OkAsync(PrivacyRead.From(settings), ct);
    }
}
