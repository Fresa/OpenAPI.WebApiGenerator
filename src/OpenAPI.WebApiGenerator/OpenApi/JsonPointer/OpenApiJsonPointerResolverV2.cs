using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal class OpenApiJsonPointerResolverV2(JsonReference openApiReference, JsonDocument document) : 
    OpenApiJsonPointerResolver(openApiReference, document, default), IOpenApiJsonPointerResolver
{
    public IOpenApiPathItemJsonPointerResolver Resolve(KeyValuePair<string, IOpenApiPathItem> path) => 
        new PathItemPointerResolver(
            Reference,
            Document, 
            Resolve("paths", path.Key));
    
    private sealed class PathItemPointerResolver(JsonReference openApiReference, JsonDocument document, JsonPointer pointer) : 
        OpenApiJsonPointerResolver(openApiReference, document, pointer), IOpenApiPathItemJsonPointerResolver
    {
        public JsonReference ResolveParameterSchemaPointer(IOpenApiParameter parameter, int index)
        {
            string[] segments = ["parameters", index.ToString()];
            var pointer = parameter switch
            {
                _ when parameter.Schema is not null => ResolveSchema(),
                _ => throw new InvalidOperationException("Parameter doesn't have a schema")
            };
            return new JsonReference(Reference.Uri.ToString(), pointer.ToString());

            JsonPointer ResolveSchema() =>
                TryResolve(segments.Append("schema"), out var schemaPointer) ? 
                    schemaPointer : 
                    Resolve(segments);
        } 
    }
}