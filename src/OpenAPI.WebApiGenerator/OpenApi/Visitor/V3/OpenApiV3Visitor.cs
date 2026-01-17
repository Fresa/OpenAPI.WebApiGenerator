using System;
using System.Collections.Generic;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;

internal sealed partial class OpenApiV3Visitor : 
    OpenApiVisitor<OpenApiDocument>, IOpenApiVisitor
{
    private OpenApiV3Visitor(OpenApiReference<OpenApiDocument> openApiReference) : base(openApiReference)
    {
        VisitPathItems();
    }

    private readonly Dictionary<IOpenApiPathItem, JsonReference> _pathItems = new ();
    
    internal static OpenApiV3Visitor Visit(OpenApiReference<OpenApiDocument> openApiReference) => 
        new(openApiReference);

    private void VisitPathItems()
    {
        foreach (var path in OpenApiDocument.Paths)
        {
            var pointer = Visit("paths", path.Key);
            _pathItems.Add(path.Value, new JsonReference(Reference.Uri, pointer.ToString().AsSpan()));
        }
    }
    
    public IOpenApiPathItemVisitor Visit(IOpenApiPathItem pathItem) => 
        PathItemVisitor.Visit(new OpenApiReference<IOpenApiPathItem>(pathItem, Document, _pathItems[pathItem]));
}