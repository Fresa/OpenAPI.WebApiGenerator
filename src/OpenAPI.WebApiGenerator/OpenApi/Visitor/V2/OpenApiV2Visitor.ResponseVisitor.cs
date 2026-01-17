using System;
using System.Collections.Generic;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V2;

internal sealed partial class OpenApiV2Visitor
{
    private sealed class ResponseVisitor :
        OpenApiVisitor<IOpenApiResponse>, IOpenApiResponseVisitor
    {
        private ResponseVisitor(OpenApiReference<IOpenApiResponse> openApiReference) : base(openApiReference)
        {
            VisitContent();
            VisitHeaders();
        }

        private JsonReference? _contentSchemaReference;
        private readonly Dictionary<IOpenApiHeader, JsonReference> _headerReferences = new();

        internal static ResponseVisitor Visit(OpenApiReference<IOpenApiResponse> openApiReference) =>
            new(openApiReference);

        private void VisitContent()
        {
            if (TryVisit(["schema"], out var schemaPointer))
            {
                _contentSchemaReference = new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan());
            }
        }

        private void VisitHeaders()
        {
            if (OpenApiDocument.Headers == null)
            {
                return;
            }
            foreach (var openApiHeader in OpenApiDocument.Headers)
            {
                var headerPointer = Visit("headers", openApiHeader.Key);
                var reference = new JsonReference(Reference.Uri, headerPointer.ToString().AsSpan());
                _headerReferences.Add(openApiHeader.Value, reference);
            }
        }
        
        public JsonReference GetSchemaReference(OpenApiMediaType mediaType) => 
            _contentSchemaReference ?? throw new InvalidOperationException("Response has no content defined");

        public bool HasContent(OpenApiMediaType mediaType) => 
            _contentSchemaReference != null;
        
        public JsonReference GetSchemaReference(IOpenApiHeader header) => 
            _headerReferences[header];
    }
}