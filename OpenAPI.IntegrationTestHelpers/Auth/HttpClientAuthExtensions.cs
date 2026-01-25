using System.Net.Http.Headers;

namespace OpenAPI.IntegrationTestHelpers.Auth;

public static class HttpClientAuthExtensions
{
    public static HttpClient WithOAuth2ImplicitFlowAuthentication(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            AuthenticationHeaderValue.Parse($"Bearer {OIDCAuthHttpHandler.Jwt}");
        return client;
    } 
}