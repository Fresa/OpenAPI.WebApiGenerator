using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace OpenAPI.IntegrationTestHelpers.Auth;

public sealed class HotWiredJwtBackchannelHandler : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.BackchannelHttpHandler = new OIDCAuthHttpHandler();
    }
}