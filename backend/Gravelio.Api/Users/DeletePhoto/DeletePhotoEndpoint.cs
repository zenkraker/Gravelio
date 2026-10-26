using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.Users;

public sealed class DeletePhotoEndpoint(AppDb db) : EndpointWithoutRequest
{
    public override void Configure() => Delete("/users/me/photo");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var me = User.UserId();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.UserPhotos.Where(p => p.UserId == me).ExecuteDeleteAsync(ct);
        await db.Users.Where(u => u.Id == me).ExecuteUpdateAsync(s => s.SetProperty(u => u.HasPhoto, false), ct);
        await transaction.CommitAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
