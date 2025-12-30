using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IOpenApiResponseVisitor
{
    public JsonReference GetSchemaReference(OpenApiMediaType mediaType);
    public bool HasContent();
    public JsonReference GetSchemaReference(IOpenApiHeader header);
}