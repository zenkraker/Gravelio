using Gravelio.Api.Common;
using System.IO.Compression;
using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.DataTransfer;

/// A zip of everything the signed-in person owns: <c>manifest.json</c>, <c>data.json</c> and one <c>activities/N.json</c> per activity
/// with its streams, written one entry at a time. Never includes a password hash, security stamp or MFA secret.
public sealed class ExportDataEndpoint(AppDb db, UserManager<User> users, TimeProvider time) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/users/me/export");
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        var user = (await users.GetUserAsync(User))!;
        var now = time.GetUtcNow().UtcDateTime;

        // Built on disk and then streamed: Kestrel forbids the synchronous writes a zip makes to the response body, and a large history
        // must not sit in memory. The temp file disappears when the stream is closed.
        var path = Path.Combine(Path.GetTempPath(), $"gravelio-export-{Guid.NewGuid():N}.zip");
        try
        {
            await BuildAsync(path, userId, user, now, ct);
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        var download = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await Send.StreamAsync(download, $"gravelio-export-{now:yyyyMMdd}.zip", download.Length, "application/zip", cancellation: ct);
    }

    async Task BuildAsync(string path, int userId, User user, DateTime now, CancellationToken ct)
    {
        await using (var file = File.Create(path))
        await using (var zip = await ZipArchive.CreateAsync(file, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: null, ct))
        {
            await WriteAsync(zip, "manifest.json", new ExportManifest(ExportJson.Format, ExportJson.Version, now), ct);
            var data = new ExportedData(
                new ExportProfile(user.Name, user.Email!, user.City, user.Birthdate, user.PreferredLanguage, user.Gender, user.Units, user.Height,
                    user.MaxHeartRate, user.FirstDayOfWeek, user.Timezone, user.Currency),
                await db.UserPrivacySettings.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct),
                await db.Gears.AsNoTracking().Include(g => g.Components).Where(g => g.UserId == userId).OrderBy(g => g.Id).ToListAsync(ct),
                await db.DefaultGears.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(ct),
                await db.Goals.AsNoTracking().Where(g => g.UserId == userId).ToListAsync(ct),
                await db.HealthWeights.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.Date).ToListAsync(ct),
                await db.HealthSteps.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.Date).ToListAsync(ct),
                await db.HealthSleeps.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.Date).ToListAsync(ct),
                await db.HealthWaters.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.Date).ToListAsync(ct),
                await db.HealthTargets.AsNoTracking().FirstOrDefaultAsync(h => h.UserId == userId, ct),
                await db.HealthFastings.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.FastStartTime).ToListAsync(ct),
                await db.HealthPoops.AsNoTracking().Where(h => h.UserId == userId).OrderBy(h => h.DateTime).ToListAsync(ct));
            await WriteAsync(zip, "data.json", data, ct);

            var ids = await db.Activities.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.StartTime).Select(a => a.Id).ToListAsync(ct);
            for (var n = 0; n < ids.Count; n++)
            {
                var id = ids[n];
                var activity = await db.Activities.AsNoTracking().Include(a => a.Streams).Include(a => a.Laps).Where(a => a.Id == id).FirstAsync(ct);
                await WriteAsync(zip, $"activities/{n + 1}.json", activity, ct);
            }
        }

    }

    static async Task WriteAsync<T>(ZipArchive zip, string name, T value, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = await entry.OpenAsync(ct);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, ExportJson.Options, ct);
    }
}
