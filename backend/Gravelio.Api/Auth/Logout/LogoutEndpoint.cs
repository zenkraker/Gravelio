using FastEndpoints;
using Gravelio.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Gravelio.Api.Auth;

public sealed class LogoutEndpoint(SignInManager<User> signIn) : EndpointWithoutRequest
{
    public override void Configure() => Post("/auth/logout");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await signIn.SignOutAsync();
        await Send.NoContentAsync(ct);
    }
}
