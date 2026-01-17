using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IOpenApiResponseVisitor
{
    public JsonReference GetSchemaReference(IOpenApiMediaType mediaType);
    public bool HasContent(IOpenApiMediaType mediaType);
    public JsonReference GetSchemaReference(IOpenApiHeader header);
}