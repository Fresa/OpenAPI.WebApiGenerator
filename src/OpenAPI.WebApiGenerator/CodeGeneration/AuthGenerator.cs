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
    
    internal SourceCode GenerateSecurityRequirementsFilter(string @namespace)
    {
        if (!_securitySchemes.Any())
        {
            return new SourceCode("SecurityRequirementsFilter.g.cs", 
$$"""
#nullable enable
using System.Security.Claims;

namespace {{@namespace}};

internal sealed class AnonymousFilter() : IEndpointFilter
{
    internal static readonly AnonymousFilter Instance = new AnonymousFilter();
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Anonymous
        context.HttpContext.User ??= new(new ClaimsIdentity());;
        return next(context);
    }
}
#nullable restore        
""");
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
        
        var passed = true;
        var passedAuthentication = true;
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
    
    internal string GenerateAuthFilter(OpenApiOperation operation)
    {
        var securityRequirementGroups =
            GetSecuritySchemeGroups(operation.Security) ?? _topLevelSecuritySchemeGroups;
        if (!securityRequirementGroups.Any())
        {
            return 
"""
internal sealed class SecurityRequirementsFilter() : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Anonymous
        context.HttpContext.User ??= new(new ClaimsIdentity());;
        return next(context);
    }
}
""";
        }

        return  
$$"""
internal sealed class SecurityRequirementsFilter(Operation operation, WebApiConfiguration configuration) : BaseSecurityRequirementsFilter(configuration)
{
    protected override SecurityRequirements Requirements { get; } = new()
    {{{string.Join(", ", 
        securityRequirementGroups.Select(securityRequirementGroup =>
            securityRequirementGroup.AggregateToString(securityRequirement => 
$$"""
        new SecurityRequirement
        {
            ["{{securityRequirement.Key}}"] = [{{string.Join(", ", securityRequirement.Value.Select(scope => $"\"{scope}\""))}}]
        }
""")))}}
    };
    
    protected override void HandleUnauthorized(HttpResponse response) => operation.HandleUnauthorized().WriteTo(response);
    protected override void HandleForbidden(HttpResponse response) => operation.HandleForbidden().WriteTo(response);
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
}
