using System;
using System.Collections.Generic;
using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;

internal sealed partial class OpenApiV3Visitor
{
    private sealed partial class PathItemVisitor : 
        OpenApiVisitor<IOpenApiPathItem>, IOpenApiPathItemVisitor
    {
        private Dictionary<IOpenApiParameter, JsonReference> _parameterSchemaReferences = new();
        private readonly Dictionary<HttpMethod, OperationVisitor> _operations = new();

        private PathItemVisitor(OpenApiReference<IOpenApiPathItem> openApiReference) : base(openApiReference)
        {
            VisitParameters();
            VisitOperations();
        }
        
        private void VisitParameters()
        {
            if (OpenApiDocument.Parameters == null)
            {
                return;
            }
            
            var parametersPointer = Visit("parameters");
            var parametersVisitor = ParametersVisitor.Visit(
                new OpenApiReference<IList<IOpenApiParameter>>(
                    OpenApiDocument.Parameters,
                    Document,
                    new JsonReference(Reference.Uri, parametersPointer.ToString().AsSpan())));
            _parameterSchemaReferences = parametersVisitor.Schemas;
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
        
        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterSchemaReferences[parameter];

        internal static PathItemVisitor Visit(OpenApiReference<IOpenApiPathItem> openApiReference) => 
            new(openApiReference);

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            _operations[httpMethod];
    }
}