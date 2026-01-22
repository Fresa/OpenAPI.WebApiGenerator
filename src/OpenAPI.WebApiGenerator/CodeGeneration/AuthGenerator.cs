using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class AuthGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly string[][] _topLevelSecuritySchemeGroups;

    public AuthGenerator(OpenApiDocument securitySchemes)
    {
        _securitySchemes = securitySchemes.Components?.SecuritySchemes ??
                           new Dictionary<string, IOpenApiSecurityScheme>();
        _topLevelSecuritySchemeGroups = GetSecuritySchemeGroups(securitySchemes.Security) ?? [];
    }
    
    internal string GenerateAuthorizationDirective(IList<OpenApiSecurityRequirement>? securityRequirements)
    {
        var requiredSecuritySchemeGroups =
            GetSecuritySchemeGroups(securityRequirements) ?? _topLevelSecuritySchemeGroups;
        
        var uniqueSecuritySchemes = requiredSecuritySchemeGroups
            .SelectMany(schemes => schemes)
            .Distinct();
        return
$$"""
.RequireAuthorization(policy =>
    policy
        .AddAuthenticationSchemes({{string.Join(", ", uniqueSecuritySchemes.Select(scheme => $"\"{scheme}\""))}})
        .RequireAssertion(context => 
            {{(requiredSecuritySchemeGroups.Any() 
                ? string.Join(" || ", requiredSecuritySchemeGroups.Select(requirement => 
                    $"({GenerateAuthenticationConditions(requirement)})"))
                : "true")}}))
""";
    }

    private static string GenerateAuthenticationConditions(string[] schemes) =>
        schemes.Any()
            ? string.Join(" && ", schemes.Select(scheme =>
                $"""context.IsAuthenticated("{scheme}")"""))
            : "true";

    internal string GenerateIsAuthenticatedExtensionMethod()
    {
        return """
        private static bool IsAuthenticated(this AuthorizationHandlerContext context, string authType) => 
            context.User.Identities.Any(identity => identity.AuthenticationType == authType && identity.IsAuthenticated);
        """;
    }

    private string[][]? GetSecuritySchemeGroups(IList<OpenApiSecurityRequirement>? securityRequirements) =>
        securityRequirements?
            .Select(requirement =>
                requirement.Keys
                    .Select(GetSecuritySchemeName)
                    .ToArray())
            .ToArray();
    private string GetSecuritySchemeName(OpenApiSecuritySchemeReference reference)
        => _securitySchemes.First(pair => pair.Value == reference.Target).Key;
}