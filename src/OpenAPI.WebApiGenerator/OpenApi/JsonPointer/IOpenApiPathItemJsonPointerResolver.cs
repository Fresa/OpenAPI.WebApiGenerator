using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiPathItemJsonPointerResolver
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index);
    IOpenApiOperationJsonPointerResolver Resolve(HttpMethod parameter);
}