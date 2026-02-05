using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class AuthGenerator
{
    private readonly IDictionary<string, IOpenApiSecurityScheme> _securitySchemes;
    private readonly Dictionary<string, List<string>>[] _topLevelSecuritySchemeGroups;

    private readonly ConcurrentDictionary<string, HashSet<(OpenApiOperation Operation, ParameterGenerator? Parameter)>> _securitySchemeParameters = new();

    private readonly Dictionary<OpenApiOperation, string[]> _requestFilters = new();
    
    public AuthGenerator(OpenApiDocument openApiDocument)
    {
        _securitySchemes = openApiDocument.Components?.SecuritySchemes ??
                           new Dictionary<string, IOpenApiSecurityScheme>();
        _topLevelSecuritySchemeGroups = GetSecuritySchemeGroups(openApiDocument.Security) ?? [];
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
        var schemeName = pair.Key;
        var className = schemeName.ToPascalCase();
        var scheme = pair.Value;
        return scheme.Type == null ? string.Empty : 
$$"""
    internal const string {{className}}Key = "{{pair.Key}}";
    internal static class {{className}}
    {{{new []
    {
        GenerateConst(nameof(scheme.Description), scheme.Description), 
        GenerateConst(nameof(scheme.Type), scheme.Type?.GetDisplayName()),
        GenerateConst(nameof(scheme.Scheme), scheme.Scheme),
        GenerateConst(nameof(scheme.BearerFormat), scheme.BearerFormat),
        GenerateConst(nameof(scheme.OpenIdConnectUrl), scheme.OpenIdConnectUrl?.ToString()),
        GenerateGetParameterMethods(schemeName, scheme),
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

    private string GenerateGetParameterMethods(string schemeName, IOpenApiSecurityScheme scheme)
    {
        if (scheme.Name == null || scheme.In == null)
        {
            return string.Empty;
        }

        var hasNonDefinedParameters = true;
        var parameterGenerators = Array.Empty<ParameterGenerator>();
        var parameterFullyQualifiedTypeNames = Array.Empty<string>();
        if (_securitySchemeParameters.TryGetValue(schemeName, out var securitySchemeParameters))
        {
            hasNonDefinedParameters = securitySchemeParameters.Any(tuple => tuple.Parameter == null);
            parameterGenerators = securitySchemeParameters
                .Where(tuple => tuple.Parameter != null)
                .Select(tuple => tuple.Parameter!)
                .ToArray();
            parameterFullyQualifiedTypeNames = parameterGenerators.Select(generator => generator.FullyQualifiedTypeName)
                .Distinct()
                .ToArray();
        }

        var securitySchemeParameterKey = parameterGenerators.Any()
            ? GetSecuritySchemeParameterKey(parameterGenerators.First())
            : null;

        return 
$$"""
{{(hasNonDefinedParameters ? 
$"""
{GenerateConst(nameof(scheme.Name), scheme.Name)}
{GenerateConst(nameof(scheme.In), scheme.In.GetDisplayName())}

""" : "")}}{{(parameterFullyQualifiedTypeNames.Length == 1 && !hasNonDefinedParameters ?
$$"""
/// <summary>
/// Get the security scheme parameter for the current operation.
/// Make sure to validate the parameter before using it. 
/// </summary>
/// <param name="context">Http context</param>
/// <returns>The security scheme parameter</returns>
internal static {{parameterFullyQualifiedTypeNames.First()}} GetParameter(HttpContext context) =>
    ({{parameterFullyQualifiedTypeNames.First()}})context.Items["{{securitySchemeParameterKey}}"];
    
""" : $$"""
private static bool TryGet<T>(HttpContext context, out T value) where T : struct
{
    if (TryGet(context, out T? nullableValue))
    {
        value = (T)nullableValue!;
    }

    value = default;
    return false;
}

private static bool TryGet<T>(HttpContext context, out T? value) where T : struct
{
    if (context.Items.TryGetValue("{{securitySchemeParameterKey}}", out var itemValue))
    {
        switch (itemValue)
        {
            case T typedValue:
                value = typedValue;
                return true;
            case null:
                value = null;
                return true;
        }
    }
    
    value = null;
    return false;
}
{{parameterFullyQualifiedTypeNames.AggregateToString(fullyQualifiedTypeName =>
$"""
/// <summary>
/// Get the security scheme parameter for the current operation if it has defined a parameter specification corresponding to the type.
/// Make sure to validate the parameter before using it. 
/// </summary>
/// <param name="context">Http context</param>
/// <param name="value">The security scheme parameter</param>
/// <returns>true if the typed security scheme parameter is found</returns>
internal static bool TryGetParameter(HttpContext context, out {fullyQualifiedTypeName} value) => 
    TryGet(context, out value);
""")}}
""")}}
""";
    }
    
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
    
    internal string[] GetSecurityFilterNames(OpenApiOperation operation) => _requestFilters[operation];
    internal SourceCode? GenerateSecurityRequirementsFilter(string @namespace)
    {
        if (!_securitySchemes.Any())
        {
            return null;
        }
        
        return new SourceCode("SecurityRequirementsFilter.g.cs", 
$$"""
#nullable enable
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace {{@namespace}};

internal abstract class BaseSecurityRequirementsFilter(WebApiConfiguration configuration) : IEndpointFilter
{
    protected abstract SecurityRequirements Requirements { get; }

    protected abstract void HandleForbidden(HttpResponse response);
    protected abstract void HandleUnauthorized(HttpResponse response);
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;
        
        var principal = httpContext.User ??= new();
        
        var passed = Requirements.Count == 0;
        var passedAuthentication = passed;
        // Only one of the security requirement objects need to be satisfied to authorize a request.
        foreach (var securityRequirement in Requirements)
        {
            var authenticated = true;
            var authorized = true;
            // Security Requirement Objects that contain multiple schemes require that all schemes MUST be satisfied for a request to be authorized.
            foreach (var (scheme, scopes) in securityRequirement)
            {
                var authenticateResult = await httpContext.AuthenticateAsync(scheme)
                    .ConfigureAwait(false);
                
                if (authenticateResult.Succeeded)
                {
                    principal.AddIdentities(authenticateResult.Principal.Identities);
                } 
                else
                {
                    authenticated = false;
                    break;
                } 
             
                authorized &= ClaimContainsScopes(authenticateResult.Principal, configuration.SecuritySchemeOptions.GetScopeOptions(scheme), scopes);
                if (!authorized)
                    break;
            }
            
            passedAuthentication |= authenticated;
            passed |= (authenticated && authorized);
        }

        if (passed)
        {
            if (!principal.Identities.Any())
            {
                // Anonymous
                principal.AddIdentity(new ClaimsIdentity());
            }
            return await next(context)
                .ConfigureAwait(false);
        }        
    
        if (passedAuthentication)
        {
            HandleForbidden(httpContext.Response);
            return null;    
        }
        
        HandleUnauthorized(httpContext.Response);
        return null;
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
    
    internal class SecurityRequirements : List<SecurityRequirement>, IAuthorizationRequirement;
    internal class SecurityRequirement : Dictionary<string, string[]>;
}
#nullable restore
""");
    }
    
    internal string GenerateAuthFilters(OpenApiOperation operation, ParameterGenerator[] parameters,
        out bool requiresAuth)
    {
        const string securitySchemeParameterFilterClassName = "SecuritySchemeParameterFilter";
        const string securityRequirementsFilterClassName = "SecurityRequirementsFilter";

        var securityRequirementGroups =
            GetSecuritySchemeGroups(operation.Security) ?? _topLevelSecuritySchemeGroups;
        requiresAuth = securityRequirementGroups.Any();
        if (!requiresAuth)
        {
            _requestFilters.Add(operation, [securityRequirementsFilterClassName]);
            return 
$$"""
internal sealed class {{securityRequirementsFilterClassName}} : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Anonymous
        context.HttpContext.User ??= new(new ClaimsIdentity());
        return next(context);
    }
}
""";
        }

        var securitySchemeParameters = GetSecuritySchemeParameters(operation, parameters);
        var hasSecuritySchemeParameters = securitySchemeParameters.Any();
        _requestFilters.Add(operation,
            hasSecuritySchemeParameters
                ? [securitySchemeParameterFilterClassName, securityRequirementsFilterClassName]
                : [securityRequirementsFilterClassName]);
        return (hasSecuritySchemeParameters ? 
$$"""
internal sealed class {{securitySchemeParameterFilterClassName}} : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = (Request) httpContext.Items[RequestItemKey]!;
{{securitySchemeParameters
    .Select(tuple => tuple.Value)
    .Distinct()
    .AggregateToString(parameterGenerator =>
$"""
        httpContext.Items.Add("{GetSecuritySchemeParameterKey(parameterGenerator)}", request.{parameterGenerator.Location.ToPascalCase()}.{parameterGenerator.PropertyName});
""")}}
        return next(context);
    }
}

""" : string.Empty) +  
$$"""

internal sealed class {{securityRequirementsFilterClassName}}(Operation operation, WebApiConfiguration configuration) : BaseSecurityRequirementsFilter(configuration)
{
    protected override SecurityRequirements Requirements { get; } = new()
    {{{string.Join(",", 
        securityRequirementGroups.Select(securityRequirementGroup =>
            securityRequirementGroup.AggregateToString(securityRequirement => 
$$"""
        new SecurityRequirement
        {
            ["{{securityRequirement.Key}}"] = [{{string.Join(", ", securityRequirement.Value.Select(scope => $"\"{scope}\""))}}]
        }
""")))}}
    };
    
    protected override void HandleUnauthorized(HttpResponse response) => operation.Validate(operation.HandleUnauthorized(), configuration).WriteTo(response);
    protected override void HandleForbidden(HttpResponse response) => operation.Validate(operation.HandleForbidden(), configuration).WriteTo(response);
}
""";
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

    private static string GetSecuritySchemeParameterKey(ParameterGenerator generator) =>
        $"OpenAPI.WebApiGenerator.SecurityScheme.{generator.Location.ToPascalCase()}.{generator.PropertyName}";

    private Dictionary<OpenApiSecuritySchemeReference, ParameterGenerator> GetSecuritySchemeParameters(OpenApiOperation operation, ParameterGenerator[] parameters)
    {
        var nullableSecuritySchemeParameters =
            operation.Security?
                .SelectMany(requirement =>
                    requirement.Where(pair => pair.Key.In != null && pair.Key.Name != null)
                        .Select(pair => pair.Key))
                .Distinct()
                .Select(reference => (Scheme: reference,
                    Parameter: parameters.FirstOrDefault(generator => generator.IsSecuritySchemeParameter(reference)) ?? null))
                .ToArray()
            ?? [];
        
        foreach (var (scheme, parameter) in nullableSecuritySchemeParameters)
        {
            _securitySchemeParameters.AddOrUpdate(GetSecuritySchemeName(scheme),
                _ => [(operation, parameter)],
                (_, list) =>
                {
                    list.Add((operation, parameter));
                    return list;
                });
        }

        return nullableSecuritySchemeParameters
            .Where(pair => pair.Parameter != null)
            .ToDictionary(pair => pair.Scheme, pair => pair.Parameter!);
    }
}
