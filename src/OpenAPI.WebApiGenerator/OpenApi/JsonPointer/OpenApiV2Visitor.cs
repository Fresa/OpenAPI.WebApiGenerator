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
        
        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterVisitor.GetSchemaReference(parameter);

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            new OperationVisitor(Reference, Document, Visit(httpMethod.Method.ToLowerInvariant()));

        private sealed class OperationVisitor(
            JsonReference openApiReference,
            JsonDocument document,
            JsonPointer pointer) :
            OpenApiVisitor(openApiReference, document, pointer), IOpenApiOperationVisitor
        {
            private readonly ParameterVisitor _parameterVisitor = new(openApiReference, document, pointer);
            
            public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
                _parameterVisitor.GetSchemaReference(parameter);
        }
    }

    private sealed class ParameterVisitor(
        JsonReference openApiReference,
        JsonDocument document,
        JsonPointer pointer) :
        OpenApiVisitor(openApiReference, document, pointer)
    {
        private Dictionary<(string Name, string In), JsonReference>? _cache;
        
        internal JsonReference GetSchemaReference(IOpenApiParameter parameter)
        {
            var name = parameter.GetName();
            var location = parameter.GetLocation();
            _cache ??= VisitParameters();
            return _cache.TryGetValue((name, location), out var reference)
                ? reference
                : throw new InvalidOperationException("parameter doesn't exist");
        }

        private Dictionary<(string Name, string Location), JsonReference> VisitParameters()
        {
            var parameters = new Dictionary<(string Name, string Location), JsonReference>();
            var parameterIndex = 0;
            string[] segments = ["parameters", parameterIndex.ToString()];
            while (TryVisit(segments, out var parameterPointer))
            {
                var parameterNameElement = JsonPointerUtilities.ResolvePointer(
                    Document,
                    parameterPointer.Append("name").ToString().AsSpan());
                var parameterName = parameterNameElement.GetString() ??
                                    throw new InvalidOperationException("parameter doesn't have a name");
                var parameterLocationElement = JsonPointerUtilities.ResolvePointer(
                    Document,
                    parameterPointer.Append("in").ToString().AsSpan());
                var parameterLocation = parameterLocationElement.GetString() ??
                                        throw new InvalidOperationException("parameter doesn't have a location");

                var pointer = TryVisit(segments.Append("schema"), out var schemaPointer)
                    ? schemaPointer
                    : parameterPointer;
                
                var schemaReference = new JsonReference(Reference.Uri.ToString(), pointer.ToString());
                parameters.Add((parameterName, parameterLocation), schemaReference);
                parameterIndex++;
                segments[1] = parameterIndex.ToString();
            }

            return parameters;
        }
    }
}