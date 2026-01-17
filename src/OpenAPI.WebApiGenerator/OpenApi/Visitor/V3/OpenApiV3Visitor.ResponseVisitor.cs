using System;
using System.Collections.Generic;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;

internal sealed partial class OpenApiV3Visitor
{
    private sealed class ResponseVisitor :
        OpenApiVisitor<IOpenApiResponse>, IOpenApiResponseVisitor
    {
        private ResponseVisitor(OpenApiReference<IOpenApiResponse> openApiReference) : base(openApiReference)
        {
            VisitContent();
            VisitHeaders();
        }

        private readonly Dictionary<IOpenApiHeader, JsonReference> _headerReferences = new();
        private readonly Dictionary<IOpenApiMediaType, JsonReference> _contentReferences = new();

        internal static ResponseVisitor Visit(OpenApiReference<IOpenApiResponse> openApiReference) =>
            new(openApiReference);

        private void VisitContent()
        {
            if (OpenApiDocument.Content == null)
            {
                return;
            }

            foreach (var content in OpenApiDocument.Content)
            {
                if (TryVisit(["content", content.Key, "schema"], out var schemaPointer))
                {
                    _contentReferences.Add(content.Value, new JsonReference(Reference.Uri,
                        schemaPointer.ToString().AsSpan()));
                }
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
                if (TryVisit(["headers", openApiHeader.Key, "schema"], out var schemaPointer))
                {
                    _headerReferences.Add(openApiHeader.Value, new JsonReference(Reference.Uri,
                        schemaPointer.ToString().AsSpan()));
                }
            }
        }
        
        public JsonReference GetSchemaReference(IOpenApiMediaType mediaType) => 
            _contentReferences[mediaType];

        public bool HasContent(IOpenApiMediaType mediaType) => 
            _contentReferences.ContainsKey(mediaType);
        
        public JsonReference GetSchemaReference(IOpenApiHeader header) => 
            _headerReferences[header];
    }
}