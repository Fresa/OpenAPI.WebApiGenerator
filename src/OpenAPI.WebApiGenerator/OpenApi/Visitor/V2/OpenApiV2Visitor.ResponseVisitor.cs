using System;
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
        }

        private JsonReference? _schemaReference;

        internal static ResponseVisitor Visit(OpenApiReference<IOpenApiResponse> openApiReference) =>
            new(openApiReference);

        private void VisitContent()
        {
            if (TryVisit(["schema"], out var schemaPointer))
            {
                _schemaReference = new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan());
            }
        }

        public JsonReference GetSchemaReference(OpenApiMediaType mediaType) => 
            _schemaReference ?? throw new InvalidOperationException("Response has no content defined");

        public bool HasContent() => _schemaReference != null;
        public JsonReference GetSchemaReference(IOpenApiHeader header)
        {
            throw new NotImplementedException();
        }
    }
}