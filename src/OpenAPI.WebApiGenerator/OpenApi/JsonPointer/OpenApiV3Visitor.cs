using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal sealed class OpenApiV3Visitor : 
    OpenApiVisitor<OpenApiDocument>, IOpenApiVisitor
{
    private OpenApiV3Visitor(OpenApiReference<OpenApiDocument> openApiReference) : base(openApiReference)
    {
        VisitPathItems();
    }

    private readonly Dictionary<IOpenApiPathItem, JsonReference> _pathItems = new ();
    
    internal static OpenApiV3Visitor Visit(OpenApiReference<OpenApiDocument> openApiReference) => 
        new(openApiReference);

    private void VisitPathItems()
    {
        foreach (var path in OpenApiDocument.Paths)
        {
            var pointer = Visit("paths", path.Key);
            _pathItems.Add(path.Value, new JsonReference(Reference.Uri, pointer.ToString().AsSpan()));
        }
    }
    
    public IOpenApiPathItemVisitor Visit(IOpenApiPathItem pathItem) => 
        PathItemVisitor.Visit(new OpenApiReference<IOpenApiPathItem>(pathItem, Document, _pathItems[pathItem]));

    private sealed class PathItemVisitor : 
        OpenApiVisitor<IOpenApiPathItem>, IOpenApiPathItemVisitor
    {
        private readonly Dictionary<IOpenApiParameter, ParameterVisitor> _parameterVisitors = new();

        private PathItemVisitor(OpenApiReference<IOpenApiPathItem> openApiReference) : base(openApiReference)
        {
            VisitParameters();
        }

        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterVisitors[parameter].Reference;

        private void VisitParameters()
        {
            foreach (var (parameter, i) in (OpenApiDocument.Parameters ?? []).WithIndex())
            {
                var parameterPointer = Visit("parameters", i.ToString());
                var parameterReference = new JsonReference(Reference.Uri, parameterPointer.ToString().AsSpan());
                _parameterVisitors.Add(parameter, ParameterVisitor.Visit(new OpenApiReference<IOpenApiParameter>(
                    parameter,
                    Document,
                    parameterReference)));
            }
        }

        internal static PathItemVisitor Visit(OpenApiReference<IOpenApiPathItem> openApiReference) => 
            new(openApiReference);

        public IOpenApiOperationVisitor Visit(HttpMethod parameter)
        {
            throw new NotImplementedException();
        }
    }
    
    private sealed class ParameterVisitor :
        OpenApiVisitor<IOpenApiParameter>
    {
        internal JsonReference SchemaReference { get; }

        private ParameterVisitor(OpenApiReference<IOpenApiParameter> openApiReference) : base(openApiReference)
        {
            SchemaReference = VisitSchema();
        }

        internal static ParameterVisitor Visit(OpenApiReference<IOpenApiParameter> reference) => new(reference);

        private JsonReference VisitSchema()
        {
            if (!TryVisit(["schema"], out var schemaPointer))
            {
                schemaPointer = Visit(
                    "content",
                    OpenApiDocument.Content?.Single().Key ??
                    throw new InvalidOperationException("Parameter doesn't contain a schema"),
                    "schema");
            } 
            
            return new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan());
        }
    }
}