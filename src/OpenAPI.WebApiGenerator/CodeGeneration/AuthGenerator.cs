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
        HasSecuritySchemes = _securitySchemes.Any();
    }

    internal bool HasSecuritySchemes { get; }
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
        GenerateConst(nameof(scheme.Type), scheme.Type?.GetDisplayName()),
        GenerateConst(nameof(scheme.Name), scheme.Name),
        GenerateConst(nameof(scheme.In), scheme.In?.GetDisplayName()),
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
        var securityRequirementGroups =
            GetSecuritySchemeGroups(securityRequirements) ?? _topLevelSecuritySchemeGroups;
        if (!securityRequirementGroups.Any())
        {
            return string.Empty;
        }
        
        var uniqueSecuritySchemes = securityRequirementGroups
            .SelectMany(schemes => schemes.Select(pair => pair.Key))
            .Distinct();
        return
$$"""

.RequireAuthorization(policy =>
    policy
        .AddAuthenticationSchemes({{string.Join(", ", uniqueSecuritySchemes.Select(scheme => $"\"{scheme}\""))}})
        .AddRequirements(
            new SecurityRequirements
            {{{string.Join(", ", securityRequirementGroups.Select(securityRequirementGroup =>
                securityRequirementGroup.AggregateToString(securityRequirement => 
$$"""
                new SecurityRequirement
                {
                    ["{{securityRequirement.Key}}"] = [{{string.Join(", ", securityRequirement.Value.Select(scope => $"\"{scope}\""))}}]
                }
""")))}}
            }))
""";
    }

    internal SourceCode? GenerateSecurityRequirementHandler(string @namespace)
    {
        if (!HasSecuritySchemes)
        {
            return null;
        }
        return new SourceCode("SecurityRequirementHandler.g.cs", 
$$"""
#nullable enable
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;

namespace {{@namespace}};
 
internal sealed class SecurityRequirementHandler(IHttpContextAccessor httpContextAccessor, WebApiConfiguration configuration)
    : AuthorizationHandler<SecurityRequirements>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SecurityRequirements securityRequirements)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            context.Fail(new AuthorizationFailureReason(this, "No HttpContext available"));
            return;
        }

        // Only one of the security requirement objects need to be satisfied to authorize a request.
        foreach (var securityRequirement in securityRequirements)
        {
            var allRequirementsPassed = true;
            // Security Requirement Objects that contain multiple schemes require that all schemes MUST be satisfied for a request to be authorized.
            foreach (var (scheme, scopes) in securityRequirement)
            {
                var authenticateResult = await httpContext.AuthenticateAsync(scheme)
                    .ConfigureAwait(false);
                allRequirementsPassed = authenticateResult.Succeeded && 
                    ClaimContainsScopes(authenticateResult.Principal, configuration.SecuritySchemeOptions.GetScopeOptions(scheme), scopes);
                if (!allRequirementsPassed)
                {
                    break;
                }
            }
            if (allRequirementsPassed)
            {
                context.Succeed(securityRequirements);
                return;
            }
        }
    }                                                                                    
     
    private static bool ClaimContainsScopes(ClaimsPrincipal? principal, SecuritySchemeOptions.ScopeOptions scopeOptions, params string[] scopes)
    {
        var foundScopes = scopeOptions.Format switch
        {
            SecuritySchemeOptions.ScopeOptions.ClaimFormat.SpaceDelimited => principal?.FindFirst(scopeOptions.Claim)?.Value?.Split(' ') ?? [],
            SecuritySchemeOptions.ScopeOptions.ClaimFormat.Array => principal?.FindAll(scopeOptions.Claim)?.Select(claim => claim.Value)?.ToArray() ?? [],
            _ => throw new InvalidOperationException($"{Enum.GetName(typeof(SecuritySchemeOptions.ScopeOptions.ClaimFormat), scopeOptions.Format)} not supported")
        };
        
        return scopes.All(scope => foundScopes.Contains(scope));
    }
}

internal sealed class SecurityRequirements : List<SecurityRequirement>, IAuthorizationRequirement;
internal sealed class SecurityRequirement : Dictionary<string, string[]>;
#nullable restore
""");
    }

    internal SourceCode? GenerateSecuritySchemeOptionsClass(string @namespace)
    {
        if (!_securitySchemes.Any())
        {
            return null;
        }
        return new SourceCode("SecuritySchemeOptions.g.cs", 
$$"""
#nullable enable
namespace {{@namespace}};

internal sealed class SecuritySchemeOptions 
{{{_securitySchemes.AggregateToString(pair => 
    $$"""
    public SecuritySchemeOption {{pair.Key.ToPascalCase()}} { get; init; } = new();
    """).Indent(4)}}

    internal ScopeOptions GetScopeOptions(string scheme) =>
        scheme switch 
        {{{_securitySchemes.AggregateToString(pair =>
$"""
            "{pair.Key}" => {pair.Key.ToPascalCase()}.Scope,
""")}}
            _ => throw new InvalidOperationException($"Scheme {scheme} is unknown")
        };
    
    internal sealed class SecuritySchemeOption
    {
        public ScopeOptions Scope {get; init; } = new() 
        {
            Claim = "scope",
            Format = ScopeOptions.ClaimFormat.SpaceDelimited
        };
    }
    
    internal sealed class ScopeOptions                                                                   
    {
        public required string Claim { get; init; }
        public required ClaimFormat Format { get; init; }

        internal enum ClaimFormat 
        {
            SpaceDelimited,
            Array
        }
    }
}
#nullable restore
""");
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
