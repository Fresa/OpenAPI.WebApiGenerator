using System;
using System.Linq;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal static class OpenApiHeaderExtensions
{
    internal static IOpenApiSchema GetSchema(this IOpenApiHeader header) =>
        header.Schema ?? header.Content?.Single().Value.Schema ?? throw new NullReferenceException("Schema or Content is required");
}