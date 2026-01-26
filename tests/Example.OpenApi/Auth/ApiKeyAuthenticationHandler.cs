using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Example.OpenApi.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authenticated = Options.In switch
        {
            "header" => Request.Headers.TryGetValue(Options.Name, out var apiKey) &&
                        !string.IsNullOrEmpty(apiKey),
            "cookie" => Request.Cookies.TryGetValue(Options.Name, out var apiKey) &&
                        !string.IsNullOrEmpty(apiKey),
            "query" => Request.Query.TryGetValue(Options.Name, out var apiKey) &&
                       !string.IsNullOrEmpty(apiKey),
            _ => throw new InvalidOperationException($"Unknown location {Options.In}")
        };
        if (!authenticated)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}