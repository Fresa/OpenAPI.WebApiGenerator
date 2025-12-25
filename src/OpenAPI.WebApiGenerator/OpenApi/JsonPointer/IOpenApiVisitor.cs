using System.Collections.Generic;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiVisitor
{
    public IOpenApiPathItemVisitor Visit(KeyValuePair<string, IOpenApiPathItem> path);
}