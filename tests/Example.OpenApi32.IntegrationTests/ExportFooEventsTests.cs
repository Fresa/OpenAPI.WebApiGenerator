using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Example.OpenApi32.IntegrationTests.Json;

namespace Example.OpenApi32.IntegrationTests;

public class ExportFooEventsTests(FooApplicationFactory app, ITestOutputHelper testOutput) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Theory]
    [InlineData("application/jsonl")]
    [InlineData("application/x-jsonlines")]
    [InlineData("application/x-ndjson")]
    [InlineData("application/json-seq")]
    [InlineData("application/geo+json-seq")]
    public async Task ExportingFooEvents_ShouldReturnOkWithSequentialJson(string mediaType)
    {
        using var client = app.CreateClient();
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = HttpMethod.Get
        };
        request.Headers.Accept.ParseAdd(mediaType);

        var result = await client.SendAsync(request, CancellationToken);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Content.Headers.ContentType?.MediaType.Should().Be(mediaType);

        var content = await result.Content.ReadAsStringAsync(CancellationToken);
        testOutput.WriteLine("Content:");
        testOutput.WriteLine(content);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim((char)0x1E))
            .ToArray();
        lines.Should().HaveCount(2);
        JsonNode.Parse(lines[0]).GetValue<string>("#/Name").Should().Be("foo1");
        JsonNode.Parse(lines[1]).GetValue<string>("#/Name").Should().Be("foo2");
    }

    [Theory]
    // Media type ranges match any supported media type; the most specific one is preferred
    [InlineData("*/*", "application/geo+json-seq")]
    [InlineData("application/*", "application/geo+json-seq")]
    // A specific accepted media type is preferred over a range with the same quality
    [InlineData("application/*, application/x-ndjson", "application/x-ndjson")]
    // Higher quality wins over declaration order
    [InlineData("application/jsonl;q=0.5, application/x-ndjson", "application/x-ndjson")]
    [InlineData("application/jsonl, application/x-ndjson;q=0.5", "application/jsonl")]
    // q=0 means not acceptable
    [InlineData("application/json-seq;q=0, application/x-ndjson;q=0.5", "application/x-ndjson")]
    // An exactly supported media type is preferred over a suffixed specialization of it
    [InlineData("application/json-seq", "application/json-seq")]
    // A suffix media type range prefers the most specific supported media type
    [InlineData("application/*+json-seq", "application/geo+json-seq")]
    public async Task ExportingFooEvents_NegotiatingAcceptedMediaType_ShouldReturnBestMatch(
        string acceptHeader, string expectedMediaType)
    {
        using var client = app.CreateClient();
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = HttpMethod.Get
        };
        request.Headers.TryAddWithoutValidation("Accept", acceptHeader);

        var result = await client.SendAsync(request, CancellationToken);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Content.Headers.ContentType?.MediaType.Should().Be(expectedMediaType);
    }
}