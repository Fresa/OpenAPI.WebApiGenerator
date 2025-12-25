using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiOperationJsonPointerResolver
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index);
}