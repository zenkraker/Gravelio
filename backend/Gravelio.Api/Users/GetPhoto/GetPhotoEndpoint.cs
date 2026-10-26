using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

/// A signed-in person's picture, as other members see it in lists and profiles. Not offered to signed-out visitors.
public sealed class GetPhotoEndpoint(AppDb db) : Endpoint<IdRequest>
{
    public override void Configure() => Get("/users/{id:int}/photo");

    public override async Task HandleAsync(IdRequest req, CancellationToken ct)
    {
        var photo = await db.UserPhotos.AsNoTracking().Where(p => p.UserId == req.Id && db.Users.Any(u => u.Id == req.Id && u.Active))
            .Select(p => new { p.Data, p.UpdatedAt }).FirstOrDefaultAsync(ct);
        if (photo is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        // Whatever was stored, the browser must treat it as a picture and nothing else.
        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        HttpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        var etag = $"\"{photo.UpdatedAt.Ticks}\"";
        HttpContext.Response.Headers.ETag = etag;
        HttpContext.Response.Headers.CacheControl = "private, no-cache"; // revalidate by ETag: a changed picture shows at once
        if (HttpContext.Request.Headers.IfNoneMatch == etag)
        {
            await Send.StatusCodeAsync(StatusCodes.Status304NotModified, ct);
            return;
        }
        await Send.BytesAsync(photo.Data, contentType: "image/jpeg", cancellation: ct);
    }
}
