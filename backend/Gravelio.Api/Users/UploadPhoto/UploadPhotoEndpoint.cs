using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Common;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

/// Sets the caller's profile picture. The SPA scales and re-encodes it to a small JPEG first; the server accepts only a JPEG of a
/// sane size and strips its metadata whatever the client did (see <see cref="JpegPhoto"/>).
public sealed class UploadPhotoEndpoint(AppDb db, TimeProvider time) : Endpoint<UploadPhotoRequest>
{
    public const long MaxUploadBytes = 1024 * 1024;

    public override void Configure()
    {
        Put("/users/me/photo");
        AllowFileUploads();
        Options(o => o.WithMetadata(UploadLimits.For(MaxUploadBytes)).RequireRateLimiting(RateLimiting.Upload));
    }

    public override async Task HandleAsync(UploadPhotoRequest req, CancellationToken ct)
    {
        await using var stream = req.File.OpenReadStream();
        var bytes = new byte[req.File.Length];
        await stream.ReadExactlyAsync(bytes, ct);
        var clean = JpegPhoto.Clean(bytes);
        if (clean is null) ThrowError(r => r.File, "The picture must be a JPEG of at most 1024 by 1024 pixels.", StatusCodes.Status400BadRequest);

        var me = User.UserId();
        var now = time.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var photo = await db.UserPhotos.FirstOrDefaultAsync(p => p.UserId == me, ct);
        if (photo is null) db.UserPhotos.Add(new UserPhoto { UserId = me, Data = clean, UpdatedAt = now });
        else (photo.Data, photo.UpdatedAt) = (clean, now);
        await db.Users.Where(u => u.Id == me).ExecuteUpdateAsync(s => s.SetProperty(u => u.HasPhoto, true), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
