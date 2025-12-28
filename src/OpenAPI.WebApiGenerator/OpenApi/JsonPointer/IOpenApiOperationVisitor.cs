using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiOperationVisitor
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter);
    public JsonReference GetSchemaReference(OpenApiMediaType requestBodyContent);
    public IOpenApiResponseVisitor Visit(IOpenApiResponse response);
}

internal interface IOpenApiResponseVisitor
{
    public JsonReference GetSchemaReference(OpenApiMediaType mediaType);
    public bool HasContent();
}