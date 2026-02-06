using Corvus.Json;
using Example.OpenApi.Auth;
using Example.OpenApi32;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication()
    .AddJwtBearer(SecuritySchemes.PetstoreAuthKey, options =>
    {
        var authority =
            new Uri(SecuritySchemes.PetstoreAuth.Flows.Implicit.AuthorizationUrl).GetLeftPart(UriPartial.Authority);
        options.Authority = authority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authority,
            ValidAudience = authority,
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        SecuritySchemes.SecretKeyKey,
        options =>
        {
            options.GetApiKey = context =>
            {
                var parameter = SecuritySchemes.SecretKey.GetParameter(context);
                return parameter.Validate(ValidationContext.ValidContext).IsValid
                    ? (true, parameter.GetString()!)
                    : (false, null);
            };
        })
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
        SecuritySchemes.BasicAuthKey,
        _ => { })
    .AddCertificate(SecuritySchemes.MutualTLSKey, options =>
    {
        options.AllowedCertificateTypes = CertificateTypes.All;
    })
    .AddCookie()
    .AddOpenIdConnect(SecuritySchemes.OpenIdConnectKey, options =>
    {
        options.Authority = SecuritySchemes.OpenIdConnect.OpenIdConnectUrl;
        options.ClientId = "example-client";
        options.SignInScheme = "Cookies";
    });

builder.AddOperations(builder.Configuration.Get<WebApiConfiguration>());
var app = builder.Build();
app.MapOperations();
app.Run();

public abstract partial class Program;
