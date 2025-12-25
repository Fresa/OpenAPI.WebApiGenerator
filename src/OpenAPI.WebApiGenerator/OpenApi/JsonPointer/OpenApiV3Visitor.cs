using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal class OpenApiV3Visitor(JsonReference openApiReference, JsonDocument document) : 
    OpenApiVisitor(openApiReference, document, default), IOpenApiVisitor
{
    public IOpenApiPathItemVisitor Visit(KeyValuePair<string, IOpenApiPathItem> path) => 
        new PathItemVisitor(
            Reference,
            Document, 
            Visit("paths", path.Key));
    
    private sealed class PathItemVisitor(JsonReference openApiReference, JsonDocument document, JsonPointer pointer) : 
        OpenApiVisitor(openApiReference, document, pointer), IOpenApiPathItemVisitor
    {
        public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index)
        {
            string[] segments = ["parameters", index.ToString()];
            var pointer = parameter switch
            {
                _ when parameter.Schema is not null => Visit(segments
                    .Append("schema")),
                _ when parameter.Content is not null => Visit(segments
                    .Append("content")
                    .Append(parameter.Content.Single().Key)
                    .Append("schema")),
                _ => throw new InvalidOperationException("Parameter doesn't have a schema")
            };
            return new JsonReference(Reference.Uri.ToString(), pointer.ToString());
        }

        public IOpenApiOperationVisitor Visit(HttpMethod parameter)
        {
            throw new NotImplementedException();
        }
    }
}