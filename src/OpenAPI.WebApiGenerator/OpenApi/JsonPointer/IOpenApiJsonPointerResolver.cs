using System.Collections.Generic;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal interface IOpenApiJsonPointerResolver
{
    public IOpenApiPathItemJsonPointerResolver Resolve(KeyValuePair<string, IOpenApiPathItem> path);
}