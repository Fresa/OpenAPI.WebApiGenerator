using System;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal static class OpenApiParameterExtensions
{
    internal static string GetTypeDeclarationIdentifier(this IOpenApiParameter parameter) => 
        parameter.GetName().ToPascalCase() + parameter.In.ToString().ToPascalCase();

    internal static string GetName(this IOpenApiParameter parameter) =>
        parameter.Name ?? throw new NullReferenceException("Name is required");
    
    internal static IOpenApiSchema GetSchema(this IOpenApiParameter parameter) =>
        parameter.Schema ?? parameter.Content?.Single().Value.Schema ?? throw new NullReferenceException("Schema or Content is required");
}