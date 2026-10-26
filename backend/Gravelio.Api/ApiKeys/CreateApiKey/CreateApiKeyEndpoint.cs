using Gravelio.Api.Common;
using FastEndpoints;
using Gravelio.Api.Auth;
using Gravelio.Api.Data;
using Gravelio.Api.Mfa;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gravelio.Api.ApiKeys;

public sealed class CreateApiKeyEndpoint(AppDb db, UserManager<User> users, TimeProvider time) : Endpoint<CreateApiKeyRequest, CreateApiKeyResponse>
{
    public override void Configure()
    {
        Post("/users/me/api-keys");
        Options(o => o.RequireRateLimiting(RateLimiting.Sensitive));
    }

    public override async Task HandleAsync(CreateApiKeyRequest req, CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        // A key can upload as this person, so making one needs the password (and the MFA code) even from a signed-in session.
        var check = await MfaCodes.StepUpAsync(users, db, time, user, req.CurrentPassword, req.Code, requireCode: false, ct);
        if (check == MfaResult.LockedOut) ThrowError(MfaCodes.LockedMessage, StatusCodes.Status429TooManyRequests);
        if (check != MfaResult.Ok) ThrowError(MfaCodes.InvalidMessage, StatusCodes.Status400BadRequest);
        if (await db.ApiKeys.CountAsync(k => k.UserId == user.Id, ct) >= ApiKeyAuth.MaxKeysPerUser)
            ThrowError($"You can have up to {ApiKeyAuth.MaxKeysPerUser} keys. Delete one you no longer use.", StatusCodes.Status409Conflict);

        var now = time.GetUtcNow().UtcDateTime;
        var (key, prefix, hash) = ApiKeyAuth.Generate();
        var entity = new ApiKey
        {
            UserId = user.Id,
            Name = req.Name.Trim(),
            Prefix = prefix,
            KeyHash = hash,
            CreatedAt = now,
            ExpiresAt = req.ExpiresInDays is { } days ? now.AddDays(days) : null,
        };
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        await Send.CreatedAtAsync<ListApiKeysEndpoint>(null, new CreateApiKeyResponse(entity.Id, entity.Name, key, prefix, entity.CreatedAt, entity.ExpiresAt), cancellation: ct);
    }
}
