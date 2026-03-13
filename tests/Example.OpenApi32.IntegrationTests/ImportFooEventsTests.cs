using System.Net;
using System.Net.Mime;
using AwesomeAssertions;
using OpenAPI.IntegrationTestHelpers.Auth;

namespace Example.OpenApi32.IntegrationTests;

public class ImportFooEventsTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task ImportingFooEvents_ShouldReturnAccepted()
    {
        using var client = app.CreateClient()
            .WithOAuth2ImplicitFlowAuthentication("update");
        var result = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1/events"),
            Method = new HttpMethod("POST"),
            Content = CreateJsonContent(
                """
                {
                    "Name": "test"
                }
                """, "application/jsonl")
        }, CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
