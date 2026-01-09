using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<OpenApiMediaType, JsonReference> _contentReferences = new();

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
                _contentReferences.Add(content.Value, new JsonReference(Reference.Uri,
                    Pointer
                        .Append(
                            "content",
                            content.Key,
                            "schema"
                        )
                        .ToString()
                        .AsSpan()));
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
                var reference = new JsonReference(Reference.Uri,
                    Pointer
                        .Append(
                            "headers",
                            openApiHeader.Key,
                            "schema")
                        .ToString()
                        .AsSpan());
                _headerReferences.Add(openApiHeader.Value, reference);
            }
        }
        
        public JsonReference GetSchemaReference(OpenApiMediaType mediaType) => 
            _contentReferences[mediaType];

        public bool HasContent() => _contentReferences.Any();
        
        public JsonReference GetSchemaReference(IOpenApiHeader header) => 
            _headerReferences[header];
    }
}