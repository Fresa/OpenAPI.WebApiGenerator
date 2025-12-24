using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal abstract class OpenApiJsonPointerResolver(
    JsonReference openApiReference,
    JsonDocument document,
    JsonPointer pointer)
{
    internal JsonReference Reference => new(openApiReference.Uri.ToString(), Pointer.ToString()) ; 
    private JsonPointer Pointer { get; } = pointer;
    protected JsonDocument Document { get; } = document;

    public static IOpenApiJsonPointerResolver V3(JsonReference openApiReference, JsonDocument openApiSpec) => 
        new OpenApiJsonPointerResolverV3(openApiReference, openApiSpec);
    public static IOpenApiJsonPointerResolver V2(JsonReference openApiReference, JsonDocument openApiSpec) => 
        new OpenApiJsonPointerResolverV2(openApiReference, openApiSpec);

    protected JsonPointer Resolve(IEnumerable<string> segments) =>
        Resolve(segments.ToArray());
    
    protected JsonPointer Resolve(params string[] segments) =>
        TryResolve(segments, out var jsonPointer)
            ? jsonPointer
            : throw new InvalidOperationException($"{jsonPointer} doesn't exist in openapi document");

    protected bool TryResolve(IEnumerable<string> segments, out JsonPointer jsonPointer) => 
        TryResolve(segments.ToArray(), out jsonPointer);

    protected bool TryResolve(string[] segments, out JsonPointer jsonPointer)
    {
        jsonPointer = Pointer;
        foreach (var segment in segments)
        {
            jsonPointer = jsonPointer.Append(segment);
            if (!JsonPointerUtilities.TryResolvePointer(Document, jsonPointer.ToString().AsSpan(), out var node))
            {
                return false;
            }

            if (JsonPointerUtilities.TryResolvePointer(node.Value, "#/$ref".AsSpan(), out var refNode))
            {
                jsonPointer = refNode.Value.ValueKind switch
                {
                    JsonValueKind.String => new JsonPointer(refNode.Value.GetString()?
                        .TrimStart('#').Split(["/"], StringSplitOptions.RemoveEmptyEntries) ?? []),
                    _ => jsonPointer
                };
            }
        }
        
        return true;            
    }
}