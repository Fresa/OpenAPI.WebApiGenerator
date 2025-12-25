using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal class OpenApiV2Visitor(JsonReference openApiReference, JsonDocument document) :
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
        private readonly ParameterVisitor _parameterVisitor = new(openApiReference, document, pointer);
        
        public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index) => 
            _parameterVisitor.GetSchemaReference(parameter, index);

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            new OperationVisitor(Reference, Document, Visit(httpMethod.Method.ToLowerInvariant()));

        private sealed class OperationVisitor(
            JsonReference openApiReference,
            JsonDocument document,
            JsonPointer pointer) :
            OpenApiVisitor(openApiReference, document, pointer), IOpenApiOperationVisitor
        {
            private readonly ParameterVisitor _parameterVisitor = new(openApiReference, document, pointer);
            
            public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index) => 
                _parameterVisitor.GetSchemaReference(parameter, index);
        }
    }

    private sealed class ParameterVisitor(
        JsonReference openApiReference,
        JsonDocument document,
        JsonPointer pointer) :
        OpenApiVisitor(openApiReference, document, pointer)
    {
        internal JsonReference GetSchemaReference(IOpenApiParameter parameter, int index)
        {
            string[] segments = ["parameters", index.ToString()];
            var pointer = parameter switch
            {
                _ when parameter.Schema is not null => VisitSchema(),
                _ => throw new InvalidOperationException("Parameter doesn't have a schema")
            };
            return new JsonReference(Reference.Uri.ToString(), pointer.ToString());

            JsonPointer VisitSchema() =>
                TryVisit(segments.Append("schema"), out var schemaPointer) ? 
                    schemaPointer : 
                    Visit(segments);
        }
    }
}