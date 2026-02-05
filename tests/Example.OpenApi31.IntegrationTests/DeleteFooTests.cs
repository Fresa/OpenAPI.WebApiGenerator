using System.Net;
using AwesomeAssertions;
using OpenAPI.IntegrationTestHelpers.Auth;

namespace Example.OpenApi31.IntegrationTests;

public class DeleteFooTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task When_Deleting_Foo_It_Should_Return_Ok()
    {
        using var client = app.CreateClient().WithValidBasicAuthCredentials();
        var result = await client.SendAsync(new HttpRequestMessage()
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1"),
            Method = new HttpMethod("DELETE")
        }, CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await result.Content.ReadAsByteArrayAsync(CancellationToken);
        responseContent.Should().BeEmpty();
        result.Content.Headers.ContentType.Should().BeNull();

        result.Headers.Should().BeEmpty();
    }
}