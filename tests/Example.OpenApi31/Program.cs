using Example.OpenApi.Auth;
using Example.OpenApi31;
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
                SecuritySchemes.SecretKey.TryGetParameter(context, out var value)
                    ? value.GetString()!
                    : throw new InvalidOperationException("");
        });

builder.AddOperations(builder.Configuration.Get<WebApiConfiguration>());
var app = builder.Build();
app.MapOperations();
app.Run();

public abstract partial class Program;