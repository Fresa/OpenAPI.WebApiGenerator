using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiVisitor
{
    public IOpenApiPathItemVisitor Visit(IOpenApiPathItem path);
}