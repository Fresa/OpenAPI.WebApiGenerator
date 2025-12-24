using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiPathItemJsonPointerResolver
{
    public JsonReference ResolveParameterSchemaPointer(IOpenApiParameter parameter, int index);
}