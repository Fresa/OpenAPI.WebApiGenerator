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
        var (isValid, value) = Options.GetApiKey(Context);
        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("invalid api key format"));
        }
        
        if (value != "password1")
        {
            return Task.FromResult(AuthenticateResult.Fail("incorrect api key"));
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}