using System;
using System.Collections.Generic;
using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal sealed class OpenApiV2Visitor :
    OpenApiVisitor<OpenApiDocument>, IOpenApiVisitor
{
    private OpenApiV2Visitor(OpenApiReference<OpenApiDocument> openApiReference) : base(openApiReference)
    {
        VisitPathItems();
    }

    private readonly Dictionary<IOpenApiPathItem, JsonReference> _pathItems = new ();
    
    internal static OpenApiV2Visitor Visit(OpenApiReference<OpenApiDocument> openApiReference) => 
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
        private readonly Dictionary<HttpMethod, OperationVisitor> _operations = new();

        private PathItemVisitor(OpenApiReference<IOpenApiPathItem> openApiReference) : base(openApiReference)
        {
            VisitParameters();
            VisitOperations();
        }

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

        private void VisitOperations()
        {
            foreach (var openApiOperation in OpenApiDocument.Operations ?? [])
            {
                var method = openApiOperation.Key;
                var operation = openApiOperation.Value; 
                var operationPointer = Visit(method.Method.ToLowerInvariant());
                var operationReference = new JsonReference(Reference.Uri, operationPointer.ToString().AsSpan());
                _operations.Add(method,
                    OperationVisitor.Visit(
                        new OpenApiReference<OpenApiOperation>(operation, Document, operationReference)));
            }
        }
        
        internal static PathItemVisitor Visit(OpenApiReference<IOpenApiPathItem> openApiReference) => 
            new(openApiReference);

        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterVisitors[parameter].Reference;

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            _operations[httpMethod];

        private sealed class OperationVisitor :
            OpenApiVisitor<OpenApiOperation>, IOpenApiOperationVisitor
        {
            private readonly Dictionary<IOpenApiParameter, ParameterVisitor> _parameterVisitors = new();
            
            private OperationVisitor(OpenApiReference<OpenApiOperation> openApiReference) : base(openApiReference)
            {
                VisitParameters();
            }
            
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

            internal static OperationVisitor Visit(
                OpenApiReference<OpenApiOperation> openApiReference) =>
                new(openApiReference);

            public JsonReference GetSchemaReference(IOpenApiParameter parameter) =>
                _parameterVisitors[parameter].SchemaReference;
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

        private JsonReference VisitSchema() =>
            TryVisit(["schema"], out var schemaPointer)
                ? new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan())
                : Reference;
    }
}
