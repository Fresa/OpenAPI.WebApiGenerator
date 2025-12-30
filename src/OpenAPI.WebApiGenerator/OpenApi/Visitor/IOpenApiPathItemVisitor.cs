using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IOpenApiPathItemVisitor
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter);
    IOpenApiOperationVisitor Visit(HttpMethod parameter);
}