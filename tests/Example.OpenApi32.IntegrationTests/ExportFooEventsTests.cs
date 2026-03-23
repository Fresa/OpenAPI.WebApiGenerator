using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Example.OpenApi32.IntegrationTests.Json;

namespace Example.OpenApi32.IntegrationTests;

public class ExportFooEventsTests(FooApplicationFactory app, ITestOutputHelper testOutput) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task ExportingFooEvents_ShouldReturnOkWithJsonl()
    {
        using var client = app.CreateClient();
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = HttpMethod.Get
        };
        request.Headers.Accept.ParseAdd("application/jsonl");

        var result = await client.SendAsync(request, CancellationToken);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Content.Headers.ContentType?.MediaType.Should().Be("application/jsonl");

        var content = await result.Content.ReadAsStringAsync(CancellationToken);
        testOutput.WriteLine("Content:");
        testOutput.WriteLine(content);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        JsonNode.Parse(lines[0]).GetValue<string>("#/Name").Should().Be("foo1");
        JsonNode.Parse(lines[1]).GetValue<string>("#/Name").Should().Be("foo2");
    }
    
    [Fact]
    public async Task ExportingFooEvents_ShouldReturnOkWithJsonSeq()
    {
        using var client = app.CreateClient();
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = HttpMethod.Get
        };
        request.Headers.Accept.ParseAdd("application/json-seq");

        var result = await client.SendAsync(request, CancellationToken);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Content.Headers.ContentType?.MediaType.Should().Be("application/json-seq");

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
