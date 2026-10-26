using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Mfa;

public sealed class GetMfaStatusEndpoint(UserManager<User> users) : EndpointWithoutRequest<GetMfaStatusResponse>
{
    public override void Configure() => Get("/users/me/mfa");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = (await users.GetUserAsync(User))!;
        await Send.OkAsync(new GetMfaStatusResponse(user.TwoFactorEnabled, await users.CountRecoveryCodesAsync(user)), ct);
    }
}
