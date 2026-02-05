using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace OpenAPI.IntegrationTestHelpers.Auth;

public sealed class OIDCAuthHttpHandler : HttpMessageHandler
{
    private static readonly SigningCredentials _privateKey;
    private const string Kid = "test";
    private static readonly RSAParameters PrivateRsaParameters;
    private static string OidcConfigurationContent { get; }
    private static string JwksContent { get; }

    static OIDCAuthHttpHandler()
    {
        using var rsa = new RSACryptoServiceProvider
        {
            PersistKeyInCsp = false
        };

        PrivateRsaParameters = rsa.ExportParameters(true);
        var securityKey = new RsaSecurityKey(PrivateRsaParameters)
        {
            KeyId = Base64UrlEncoder.Encode(Kid)
        };

        _privateKey = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256Signature);

        OidcConfigurationContent = CreateOidcConfigurationContent();
        JwksContent = CreateJwksContent();
    }

    public static string GetJwt(params string[] scopes) => GenerateJwtToken(scopes);
    internal const string Issuer = "https://localhost/";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri == null ||
            !request.RequestUri.AbsoluteUri.StartsWith(Issuer))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        return request.RequestUri.AbsolutePath switch
        {
            "/.well-known/openid-configuration" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OidcConfigurationContent)
            }),
            "/oauth/jwks" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JwksContent)
            }),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))
        };
    }

    private static string GenerateJwtToken(params string[] scopes)
    {
        var securityTokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            Subject = new ClaimsIdentity(),
            Expires = DateTime.UtcNow.AddHours(1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = _privateKey,
            Claims = new Dictionary<string, object>
            {
                ["scope"] = string.Join(" ", scopes)
            }
        };

        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        var jwt = jwtSecurityTokenHandler.CreateToken(securityTokenDescriptor);
        var token = jwtSecurityTokenHandler.WriteToken(jwt);
        return token;
    }

    private static string CreateJwksContent() =>
        $$"""
          {
            "keys" : [ 
              {
                "kid": "{{Base64UrlEncoder.Encode(Kid)}}",
                "e": "{{Base64UrlEncoder.Encode(PrivateRsaParameters.Exponent)}}",
                "kty": "RSA",
                "alg": "RS256",
                "n": "{{Base64UrlEncoder.Encode(PrivateRsaParameters.Modulus)}}"
              }
            ]
          }
          """;

    private static string CreateOidcConfigurationContent() =>
        $$"""
          {
            "issuer":"{{Issuer}}",
            "authorization_endpoint":"{{Issuer}}oauth/auz/authorize",
            "token_endpoint":"{{Issuer}}oauth/oauth20/token",
            "userinfo_endpoint":"{{Issuer}}/oauth/userinfo",
            "jwks_uri":"{{Issuer}}oauth/jwks",
            "scopes_supported":[
              "READ",
              "WRITE",
              "DELETE",
              "openid",
              "scope",
              "profile",
              "email",
              "address",
              "phone"
            ],
            "response_types_supported":[
              "code",
              "code id_token",
              "code token",
              "code id_token token",
              "token",
              "id_token",
              "id_token token"
            ],
            "grant_types_supported":[
              "authorization_code",
              "implicit",
              "client_credentials",
              "urn:ietf:params:oauth:grant-type:jwt-bearer"
            ],
            "subject_types_supported":[
              "public"
            ],
            "id_token_signing_alg_values_supported":[
              "RS256"
            ],
            "id_token_encryption_alg_values_supported":[
              "RSA-OAEP",
              "RSA-OAEP-256"
            ],
            "id_token_encryption_enc_values_supported":[
              "A256GCM"
            ],
            "token_endpoint_auth_methods_supported":[
              "client_secret_post",
              "client_secret_basic",
              "client_secret_jwt",
              "private_key_jwt"
            ],
            "token_endpoint_auth_signing_alg_values_supported":[
              "RS256"
            ]
          }
          """;
}