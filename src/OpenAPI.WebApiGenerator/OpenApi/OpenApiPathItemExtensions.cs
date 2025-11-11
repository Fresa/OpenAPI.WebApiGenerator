using System.Collections.Generic;
using System.Net.Http;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal static class OpenApiPathItemExtensions
{
    internal static Dictionary<HttpMethod, OpenApiOperation> GetOperations(this IOpenApiPathItem pathItem) =>
        pathItem.Operations ?? [];
}