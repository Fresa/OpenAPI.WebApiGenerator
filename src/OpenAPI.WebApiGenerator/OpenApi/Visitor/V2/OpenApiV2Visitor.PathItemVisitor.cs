using System;
using System.Collections.Generic;
using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V2;

internal sealed partial class OpenApiV2Visitor
{
    private sealed class PathItemVisitor : 
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
        
        internal static PathItemVisitor Visit(OpenApiReference<IOpenApiPathItem> openApiReference) => 
            new(openApiReference);

        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterSchemaReferences[parameter];

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            _operations[httpMethod];

        private sealed class OperationVisitor :
            OpenApiVisitor<OpenApiOperation>, IOpenApiOperationVisitor
        {
            private Dictionary<IOpenApiParameter, JsonReference> _parameterSchemaReferences = new();
            private JsonReference? _bodySchemaReference;
            private Dictionary<IOpenApiResponse, IOpenApiResponseVisitor> _responseVisitors = new();
            
            private OperationVisitor(OpenApiReference<OpenApiOperation> openApiReference) : base(openApiReference)
            {
                VisitParameters();
                VisitResponses();
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
                _bodySchemaReference = parametersVisitor.BodySchema;
            }

            private void VisitResponses()
            {
                foreach (var response in OpenApiDocument.Responses ?? [])
                {
                    var responsePointer = Visit("responses", response.Key);
                    var responseReference = new JsonReference(Reference.Uri, responsePointer.ToString().AsSpan());
                    var responseVisitor =
                        ResponseVisitor.Visit(
                            new OpenApiReference<IOpenApiResponse>(response.Value, Document, responseReference));
                    _responseVisitors.Add(response.Value, responseVisitor);
                }
            }
            
            internal static OperationVisitor Visit(
                OpenApiReference<OpenApiOperation> openApiReference) =>
                new(openApiReference);

            public JsonReference GetSchemaReference(IOpenApiParameter parameter) =>
                _parameterSchemaReferences[parameter];

            public JsonReference GetSchemaReference(OpenApiMediaType requestBodyContent) => 
                _bodySchemaReference ?? throw new InvalidOperationException("Operation doesn't define a body");

            public IOpenApiResponseVisitor Visit(IOpenApiResponse response) => 
                _responseVisitors[response];
        }
    }
}