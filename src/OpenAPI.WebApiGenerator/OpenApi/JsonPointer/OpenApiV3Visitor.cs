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
        private readonly ParameterVisitor _parameterVisitor = new(openApiReference, document, pointer);
        
        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterVisitor.GetSchemaReference(parameter);

        public IOpenApiOperationVisitor Visit(HttpMethod parameter)
        {
            throw new NotImplementedException();
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

                if (!TryVisit(segments.Append("schema"), out var pointer))
                {
                    var contentPointer = Visit(segments
                        .Append("content"));
                    var content = JsonPointerUtilities.ResolvePointer(
                        Document, 
                        contentPointer.ToString().AsSpan());
                    var contentName = content.EnumerateObject().First().Name;
                    pointer = Visit(segments
                        .Append("content")
                        .Append(contentName)
                        .Append("schema"));
                }
                
                var schemaReference = new JsonReference(Reference.Uri.ToString(), pointer.ToString());
                parameters.Add((parameterName, parameterLocation), schemaReference);
                parameterIndex++;
                segments[1] = parameterIndex.ToString();
            }

            return parameters;
        }
    }
}