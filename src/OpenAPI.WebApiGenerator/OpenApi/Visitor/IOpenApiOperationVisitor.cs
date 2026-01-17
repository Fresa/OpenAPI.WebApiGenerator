using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IOpenApiOperationVisitor : IVisitor
{
    public JsonReference GetSchemaReference(IOpenApiParameter parameter);
    public JsonReference GetSchemaReference(IOpenApiMediaType requestBodyContent);
    public IOpenApiResponseVisitor Visit(IOpenApiResponse response);
}