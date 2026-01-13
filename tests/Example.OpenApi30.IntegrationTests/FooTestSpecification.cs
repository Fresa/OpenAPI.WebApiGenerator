using System.Text;

namespace Example.OpenApi30.IntegrationTests;

public abstract class FooTestSpecification
{
    protected CancellationToken CancellationToken { get; } = TestContext.Current.CancellationToken;

    protected HttpContent CreateJsonContent(string json) => new StringContent(
        json,
        encoding: Encoding.UTF8,
        mediaType: "application/json");
}
