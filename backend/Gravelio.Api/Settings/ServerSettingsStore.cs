using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Settings;

public sealed record ServerSettingsRead(bool SignupEnabled, bool SignupRequireEmailVerification, bool SignupRequireAdminApproval,
    bool PublicShareableLinks, bool PublicShareableLinksUserInfo, bool EmailConfigured)
{
    public static ServerSettingsRead From(ServerSettings settings, bool emailConfigured) =>
        new(settings.SignupEnabled, settings.SignupRequireEmailVerification, settings.SignupRequireAdminApproval,
            settings.PublicShareableLinks, settings.PublicShareableLinksUserInfo, emailConfigured);
}

public sealed record PublicSettingsRead(bool SignupEnabled, bool PublicShareableLinks);

public static class ServerSettingsStore
{
    /// A server that never saved its settings has them all at their defaults.
    public static async Task<ServerSettings> GetAsync(AppDb db, CancellationToken ct) =>
        await db.ServerSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == ServerSettings.SingletonId, ct)
        ?? new ServerSettings();

    /// Upsert of the singleton row, so the first save creates it.
    public static async Task<ServerSettings> SaveAsync(AppDb db, Action<ServerSettings> apply, CancellationToken ct)
    {
        var settings = await db.ServerSettings.FirstOrDefaultAsync(s => s.Id == ServerSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new ServerSettings();
            db.ServerSettings.Add(settings);
        }
        apply(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }
}
