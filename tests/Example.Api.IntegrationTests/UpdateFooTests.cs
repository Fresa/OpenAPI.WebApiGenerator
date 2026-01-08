using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Example.Api.IntegrationTests.Http;
using Example.Api.IntegrationTests.Json;

namespace Example.Api.IntegrationTests;

public class UpdateFooTests(FooApplicationFactory app) : FooTestSpecification, IClassFixture<FooApplicationFactory>
{
    [Fact]
    public async Task When_Updating_Foo_It_Should_Return_Updated_Foo()
    {
        using var client = app.CreateClient();
        var result = await client.SendAsync(new HttpRequestMessage()
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/1"),
            Method = new HttpMethod("PUT"),
            Content = CreateJsonContent(
                """
                {
                    "Name": "test"
                }
                """),
            Headers =
            {
                { "Bar", "test" }
            }
        }, CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await result.Content.ReadAsJsonNodeAsync(CancellationToken);
        responseContent.Should().NotBeNull();
        responseContent.GetValue<string>("#/Name").Should().Be("test");
        result.Headers.Should().HaveCount(1);
        result.Headers.Should().ContainKey("Status")
            .WhoseValue.Should().HaveCount(1)
            .And.Contain("2");
        result.Content.Headers.ContentType.Should().Be(MediaTypeHeaderValue.Parse("application/json"));
    }
    
    [Fact]
    public async Task Given_invalid_request_When_Updating_Foo_It_Should_Return_400()
    {
        using var client = app.CreateClient();
        var result = await client.SendAsync(new HttpRequestMessage()
        {
            RequestUri = new Uri(client.BaseAddress!, "/foo/test"),
            Method = new HttpMethod("PUT"),
            Content = CreateJsonContent(
                """
                {
                    "Name": "test"
                }
                """),
            Headers =
            {
                { "Bar", "test" }
            }
        }, CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var responseContent = await result.Content.ReadAsJsonNodeAsync(CancellationToken);
        responseContent.Should().NotBeNull();
        responseContent.AsArray().Should().HaveCount(1);
        responseContent.GetValue<string>("#/0/error").Should().NotBeNullOrEmpty();
        responseContent.GetValue<string>("#/0/name").Should().Be("#/parameters/FooId/type");
    }
}