using System.Collections.Generic;
using System.Net.Http;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class OperationRouterGenerator(string @namespace, AuthGenerator authGenerator)
{
    internal SourceCode ForMinimalApi(List<(string Namespace, KeyValuePair<HttpMethod, OpenApiOperation> Operation)> operations) =>
        new("OperationRouter.g.cs",
$$"""
#nullable enable
using Microsoft.AspNetCore.Authorization;

namespace {{@namespace}};

internal static class OperationRouter
{
    internal static WebApplication MapOperations(this WebApplication app{{(authGenerator.HasSecuritySchemes ? ", SecuritySchemeOptions? securitySchemeOptions = null" : "")}})
    {{{(authGenerator.HasSecuritySchemes ? 
"""
        
        securitySchemeOptions ??= new();
""" : "")}}
{{operations.AggregateToString(
"""
        app.UseAuthentication();
        app.UseAuthorization();

""",
        operation => 
$"""
        app.MapMethods({operation.Namespace}.Operation.PathTemplate, ["{operation.Operation.Key.Method}"], {operation.Namespace}.Operation.HandleAsync)
{authGenerator.GenerateAuthorizationDirective(operation.Operation.Value.Security).Indent(12)};
""")}}
        return app;
    }
    
    internal static WebApplicationBuilder AddOperations(this WebApplicationBuilder builder, WebApiConfiguration? configuration = null)
    {
{{operations.AggregateToString(
"""
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

""",
        operation => 
$"""
        builder.Services.AddScoped<{operation.Namespace}.Operation>();
""")}}
        builder.Services.AddSingleton(configuration ?? new());
        return builder;
    }
    
{{$"""
{authGenerator.GenerateIsAuthenticatedExtensionMethod().Indent(4)}

{authGenerator.GenerateScopeClaimExtensionMethod().Indent(4)}
""".TrimEnd()}}
}
#nullable restore
""");
}