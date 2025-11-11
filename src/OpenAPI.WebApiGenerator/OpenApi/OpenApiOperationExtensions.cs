using System.Collections.Generic;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal static class OpenApiOperationExtensions
{
    internal static IList<IOpenApiParameter> GetParameters(this OpenApiOperation operation) =>
        operation.Parameters ?? [];
}