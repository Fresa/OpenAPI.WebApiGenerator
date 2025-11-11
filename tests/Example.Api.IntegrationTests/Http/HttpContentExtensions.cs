using System.Text.Json.Nodes;

namespace Example.Api.IntegrationTests.Http;

internal static class HttpContentExtensions
{
    internal static async Task<JsonNode?> ReadAsJsonNodeAsync(this HttpContent content,
        CancellationToken cancellationToken) =>
        await JsonNode.ParseAsync(
                await content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
}