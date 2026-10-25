using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Privacy;

public sealed class UpdatePrivacySettingsEndpoint(AppDb db) : Endpoint<PrivacySettingsRequest, PrivacyRead>
{
    public override void Configure() => Put("/users/me/privacy");

    public override async Task HandleAsync(PrivacySettingsRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var settings = await db.UserPrivacySettings.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (settings is null)
        {
            settings = new UserPrivacySettings { UserId = userId };
            db.UserPrivacySettings.Add(settings);
        }
        settings.Apply(req);
        if (!await db.TrySaveUniqueAsync(ct)) ThrowError("Privacy settings were changed at the same time. Try again.", StatusCodes.Status409Conflict);
        await Send.OkAsync(PrivacyRead.From(settings), ct);
    }
}
