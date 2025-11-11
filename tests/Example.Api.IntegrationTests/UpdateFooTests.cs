using System.Net;
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
        var result = await client.PutAsync("/foo",
            CreateJsonContent(
                """
                {
                    "Name": "test"
                }
                """), CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await result.Content.ReadAsJsonNodeAsync(CancellationToken);
        responseContent.Should().NotBeNull();
        responseContent.GetValue<string>("#/Name").Should().Be("test");
    }
}