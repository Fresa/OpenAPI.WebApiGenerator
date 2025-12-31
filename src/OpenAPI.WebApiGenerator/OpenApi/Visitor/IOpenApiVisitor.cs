using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IOpenApiVisitor
{
    public IOpenApiPathItemVisitor Visit(IOpenApiPathItem path);
}