using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiOperationVisitor
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index);
}