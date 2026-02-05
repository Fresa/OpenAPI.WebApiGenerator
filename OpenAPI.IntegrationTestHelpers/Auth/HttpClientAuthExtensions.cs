using System.Net.Http.Headers;

namespace OpenAPI.IntegrationTestHelpers.Auth;

public static class HttpClientAuthExtensions
{
    public static HttpClient WithOAuth2ImplicitFlowAuthentication(this HttpClient client, params string[] scopes)
    {
        client.DefaultRequestHeaders.Authorization =
            AuthenticationHeaderValue.Parse($"Bearer {OIDCAuthHttpHandler.GetJwt(scopes)}");
        return client;
    } 
    
    public static HttpClient WithValidBasicAuthCredentials(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String("admin:password"u8.ToArray()));
        return client;
    }
}