using System;
using System.Collections.Generic;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;

internal sealed partial class OpenApiV3Visitor
{
    private sealed partial class PathItemVisitor
    {
        private sealed class OperationVisitor :
            OpenApiVisitor<OpenApiOperation>, IOpenApiOperationVisitor
        {
            private Dictionary<IOpenApiParameter, JsonReference> _parameterSchemaReferences = new();
            private readonly Dictionary<IOpenApiResponse, IOpenApiResponseVisitor> _responseVisitors = new();
            private readonly Dictionary<IOpenApiMediaType, JsonReference> _requestContentSchemaReferences = new();
            
            private OperationVisitor(OpenApiReference<OpenApiOperation> openApiReference) : base(openApiReference)
            {
                VisitParameters();
                VisitResponses();
                VisitRequestBody();
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
            
            private void VisitRequestBody()
            {
                if (OpenApiDocument.RequestBody?.Content == null)
                {
                    return;
                }
                
                var requestContentPointer = Visit("requestBody", "content");
                foreach (var content in OpenApiDocument.RequestBody.Content)
                {
                    _requestContentSchemaReferences.Add(content.Value,
                        new JsonReference(Reference.Uri,
                            requestContentPointer
                                .Append(content.Key)
                                .Append(content.Value.ItemSchema is not null ? "itemSchema" : "schema")
                                .ToString()
                                .AsSpan()));
                }
            }
            
            internal static OperationVisitor Visit(
                OpenApiReference<OpenApiOperation> openApiReference) =>
                new(openApiReference);

            public JsonReference GetSchemaReference(IOpenApiParameter parameter) =>
                _parameterSchemaReferences[parameter];

            public JsonReference GetSchemaReference(IOpenApiMediaType mediaType) => 
                _requestContentSchemaReferences[mediaType];

            public IOpenApiResponseVisitor Visit(IOpenApiResponse response) => 
                _responseVisitors[response];
        }
    }
}