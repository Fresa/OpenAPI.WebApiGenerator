using System.Text;

namespace Example.OpenApi32.IntegrationTests;

public abstract class FooTestSpecification
{
    protected CancellationToken CancellationToken { get; } = TestContext.Current.CancellationToken;

    protected HttpContent CreateJsonContent(string json, string mediaType = "application/json") => new StringContent(
        json,
        encoding: Encoding.UTF8,
        mediaType: mediaType);
}
