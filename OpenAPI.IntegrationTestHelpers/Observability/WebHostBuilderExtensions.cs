using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenAPI.IntegrationTestHelpers.Observability;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder AddLogging(this IWebHostBuilder builder) =>
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Trace);

            logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Trace);
            logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Trace);
            logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Trace);
        });
}