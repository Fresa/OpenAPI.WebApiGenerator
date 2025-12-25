using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiPathItemVisitor
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter, int index);
    IOpenApiOperationVisitor Visit(HttpMethod parameter);
}