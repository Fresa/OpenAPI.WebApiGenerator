using Microsoft.AspNetCore.Authentication;

namespace Example.OpenApi.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string In { get; set; } = "Header";
    public string Name { get; set; } = "X-Api-Key";
}