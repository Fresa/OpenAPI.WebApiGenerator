using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Example.OpenApi.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public Func<HttpContext, string> GetApiKey { get; set; } = _ => throw new InvalidOperationException("Missing api key handler");
}