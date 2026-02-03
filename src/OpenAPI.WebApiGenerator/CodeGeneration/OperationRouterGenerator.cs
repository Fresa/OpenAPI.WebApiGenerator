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
    internal static WebApplication MapOperations(this WebApplication app)
    {{{operations.AggregateToString(operation => 
$"""
        app.MapMethods({operation.Namespace}.Operation.PathTemplate, ["{operation.Operation.Key.Method}"], {operation.Namespace}.Operation.HandleAsync)
            .AddEndpointFilter<{operation.Namespace}.Operation.BindRequestFilter>()
{authGenerator.GetSecurityFilterNames(operation.Operation.Value).AggregateToString(name => 
$"""
            .AddEndpointFilter<{operation.Namespace}.Operation.{name}>()
""")};
""")}}
        return app;
    }
    
    internal static WebApplicationBuilder AddOperations(this WebApplicationBuilder builder, WebApiConfiguration? configuration = null)
    {{{operations.AggregateToString(operation => 
$"""
        builder.Services.AddScoped<{operation.Namespace}.Operation>();
""")}}
        builder.Services.AddSingleton(configuration ?? new());
        return builder;
    }
}
#nullable restore
""");
}