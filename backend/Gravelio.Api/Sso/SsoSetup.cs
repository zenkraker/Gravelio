using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Gravelio.Api.Sso;

public static class SsoSetup
{
    public static IServiceCollection AddGravelioSso(this IServiceCollection services)
    {
        services.AddSingleton<SsoProviders>();
        services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>>(sp => sp.GetRequiredService<SsoProviders>());
        services.AddHostedService(sp => sp.GetRequiredService<SsoProviders>());
        // No scheme is added at start-up (providers come from the database), so the handler's usual option set-up is added by hand.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());
        return services;
    }
}
