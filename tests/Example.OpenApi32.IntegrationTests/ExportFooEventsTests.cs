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
}