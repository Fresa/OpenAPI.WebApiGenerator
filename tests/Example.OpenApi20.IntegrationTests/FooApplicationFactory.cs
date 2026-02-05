using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenAPI.IntegrationTestHelpers.Auth;
using OpenAPI.IntegrationTestHelpers.Observability;

namespace Example.OpenApi20.IntegrationTests;

[UsedImplicitly]
public class FooApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.InjectJwtBackChannelHandler();
        });

        builder.AddLogging();
    }
}
