using System.Net;
using AwesomeAssertions;
using OpenAPI.IntegrationTestHelpers.Auth;

namespace Example.OpenApi32.IntegrationTests;

public class ImportFooEventsTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Theory]
    [InlineData("application/jsonl")]
    [InlineData("application/x-jsonlines")]
    [InlineData("application/x-ndjson")]
    [InlineData("application/json-seq", "\x1E")]
    [InlineData("application/geo+json-seq", "\x1E")]
    public async Task ImportingFooEvents_ShouldReturnAccepted(string mediaType, string? prefix = "")
    {
        using var client = app.CreateClient()
            .WithOAuth2ImplicitFlowAuthentication("update");
        var result = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = new HttpMethod("POST"),
            Content = CreateJsonContent(
                $$"""
                {{prefix}}{ "Name": "test" }
                {{prefix}}{ "Name": "another test" }
                
                """, mediaType)
        }, CancellationToken);
        
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        result.Headers.Should().HaveCount(1);
        result.Headers.GetValues("ImportedEvents")
            .Should().HaveCount(1).And.AllBe("2");
    }
}
