using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class AuthGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<string, List<string>>[] _topLevelSecuritySchemeGroups;

    public AuthGenerator(OpenApiDocument securitySchemes)
    {
        _securitySchemes = securitySchemes.Components?.SecuritySchemes ??
                           new Dictionary<string, IOpenApiSecurityScheme>();
        _topLevelSecuritySchemeGroups = GetSecuritySchemeGroups(securitySchemes.Security) ?? [];
    }

    internal SourceCode? GenerateSecuritySchemeClass(string @namespace)
    {
        if (!_securitySchemes.Any())
        {
            return null;
        }
        return new SourceCode("SecuritySchemes.g.cs", 
$$"""
using System.Collections.Immutable;

namespace {{@namespace}};

internal static class SecuritySchemes 
{{{_securitySchemes.AggregateToString(pair =>
    {
        var className = pair.Key.ToPascalCase();
        var scheme = pair.Value;
        return scheme.Type == null ? string.Empty : 
$$"""
    internal const string {{className}}Key = "{{pair.Key}}";
    internal static class {{className}}
    {{{new []
    {
        GenerateConst(nameof(scheme.Description), scheme.Description), 
        GenerateConst(nameof(scheme.Type), GetEnumName(scheme.Type)),
        GenerateConst(nameof(scheme.Name), scheme.Name),
        GenerateConst(nameof(scheme.In), GetEnumName(scheme.In)),
        GenerateConst(nameof(scheme.Scheme), scheme.Scheme),
        GenerateConst(nameof(scheme.BearerFormat), scheme.BearerFormat),
        GenerateConst(nameof(scheme.OpenIdConnectUrl), scheme.OpenIdConnectUrl?.ToString()),
        $"internal const bool {nameof(scheme.Deprecated)} = {scheme.Deprecated.ToString().ToLowerInvariant()};",
        GenerateFlowsObject(nameof(scheme.Flows), scheme.Flows)
    }.RemoveEmptyLines().AggregateToString().Indent(8)}}
    }                                            
""";
    })}}
}
""");
    }

    private static string? GetEnumName<T>(T? value) where T : struct, Enum => 
        value == null ? null : Enum.GetName(typeof(T), value);

    private static string GenerateConst(string name, string? value) =>
        value == null
            ? string.Empty
            : $"""
               internal const string {name} = "{value}";
               """;

    private static string GenerateFlowsObject(string className, OpenApiOAuthFlows? flows) =>
        flows == null ? string.Empty : 
$$"""
internal static class {{className}}
{{{new []
{
    GenerateFlowObject(nameof(flows.AuthorizationCode), flows.AuthorizationCode),
    GenerateFlowObject(nameof(flows.ClientCredentials), flows.ClientCredentials),
    GenerateFlowObject(nameof(flows.DeviceAuthorization), flows.DeviceAuthorization),
    GenerateFlowObject(nameof(flows.Implicit), flows.Implicit),
    GenerateFlowObject(nameof(flows.Password), flows.Password)
}.RemoveEmptyLines().AggregateToString().Indent(4)}}
}
""";

    private static string GenerateFlowObject(string className, OpenApiOAuthFlow? flow) =>
        flow == null ? string.Empty : 
$$"""
internal static class {{className}}
{{{new []
{
    GenerateConst(nameof(flow.AuthorizationUrl), flow.AuthorizationUrl?.ToString()),
    GenerateConst(nameof(flow.DeviceAuthorizationUrl), flow.DeviceAuthorizationUrl?.ToString()),
    GenerateConst(nameof(flow.RefreshUrl), flow.RefreshUrl?.ToString()),
    GenerateConst(nameof(flow.TokenUrl), flow.TokenUrl?.ToString()),
    flow.Scopes == null ? string.Empty : 
$$"""
internal static readonly ImmutableDictionary<string, string> {{nameof(flow.Scopes)}} = 
    ImmutableDictionary.CreateRange<string, string>([{{flow.Scopes.AggregateToString(scope => 
$"""
        new("{scope.Key}", "{scope.Value}"),
""").TrimEnd(',')}}
]);
"""
}.RemoveEmptyLines().AggregateToString().Indent(4)}}
}
""";
    
    internal string GenerateAuthorizationDirective(IList<OpenApiSecurityRequirement>? securityRequirements)
    {
        var requiredSecuritySchemeGroups =
            GetSecuritySchemeGroups(securityRequirements) ?? _topLevelSecuritySchemeGroups;
        
        var uniqueSecuritySchemes = requiredSecuritySchemeGroups
            .SelectMany(schemes => schemes.Select(pair => pair.Key))
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

    private static string GenerateAuthenticationConditions(Dictionary<string, List<string>> schemes) =>
        schemes.Any()
            ? string.Join(" && ", schemes.Select(scheme =>
                $"context.IsAuthenticated(\"{scheme.Key}\") && " +
                $"context.ClaimContainsScopes(scopeClaim, {scheme.Value.AsParams()})"))
            : "true";

    internal string GenerateIsAuthenticatedExtensionMethod()
    {
        return """
        private static bool IsAuthenticated(this AuthorizationHandlerContext context, string authType) => 
            context.User.Identities.Any(identity => identity.AuthenticationType == authType && identity.IsAuthenticated);
        """;
    }

    internal string GenerateScopeClaimExtensionMethod()
    {
        return """
               private static bool ClaimContainsScopes(this AuthorizationHandlerContext context, string claim, params string[] scopes)
               { 
                   var foundScopes = context.User.FindFirst(claim)?.Value?.Split(' ') ?? [];
                   return scopes.Aggregate(true, (result, scope) => result && foundScopes.Contains(scope));
                }
               """;
    }

    private Dictionary<string, List<string>>[]? GetSecuritySchemeGroups(IList<OpenApiSecurityRequirement>? securityRequirements) =>
        securityRequirements?
            .Select(requirement =>
                requirement.ToDictionary(
                    pair => GetSecuritySchemeName(pair.Key), 
                    pair => pair.Value))
            .ToArray();
    private string GetSecuritySchemeName(OpenApiSecuritySchemeReference reference)
        => _securitySchemes.First(pair => pair.Value == reference.Target).Key;
}
