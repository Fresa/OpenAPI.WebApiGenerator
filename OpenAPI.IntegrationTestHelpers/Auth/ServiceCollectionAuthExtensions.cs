using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenAPI.IntegrationTestHelpers.Auth;

public static class ServiceCollectionAuthExtensions
{
    public static IServiceCollection InjectJwtBackChannelHandler(this IServiceCollection services)
    {
        services.Insert(0,
            ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, HotWiredJwtBackchannelHandler>());
        return services;
    }
}