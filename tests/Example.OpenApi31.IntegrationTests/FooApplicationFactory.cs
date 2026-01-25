using System.Net.Http.Headers;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAPI.IntegrationTestHelpers.Auth;
using OpenAPI.IntegrationTestHelpers.Observability;

namespace Example.OpenApi31.IntegrationTests;

[UsedImplicitly]
public class FooApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.Insert(0,
                ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, HotWiredJwtBackchannelHandler>());
        });

        builder.AddLogging();
    }
    
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization =
            AuthenticationHeaderValue.Parse($"Bearer {OIDCAuthHttpHandler.Jwt}");
    }
}