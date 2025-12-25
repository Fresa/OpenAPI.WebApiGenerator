using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal abstract class OpenApiVisitor(
    JsonReference openApiReference,
    JsonDocument document,
    JsonPointer pointer)
{
    internal JsonReference Reference => new(openApiReference.Uri.ToString(), Pointer.ToString()) ; 
    private JsonPointer Pointer { get; } = pointer;
    protected JsonDocument Document { get; } = document;

    public static IOpenApiVisitor V3(JsonReference openApiReference, JsonDocument openApiSpec) => 
        new OpenApiV3Visitor(openApiReference, openApiSpec);
    public static IOpenApiVisitor V2(JsonReference openApiReference, JsonDocument openApiSpec) => 
        new OpenApiV2Visitor(openApiReference, openApiSpec);

    protected JsonPointer Visit(IEnumerable<string> segments) =>
        Visit(segments.ToArray());
    
    protected JsonPointer Visit(params string[] segments) =>
        TryVisit(segments, out var jsonPointer)
            ? jsonPointer
            : throw new InvalidOperationException($"{jsonPointer} doesn't exist in openapi document");

    protected bool TryVisit(IEnumerable<string> segments, out JsonPointer jsonPointer) => 
        TryVisit(segments.ToArray(), out jsonPointer);

    protected bool TryVisit(string[] segments, out JsonPointer jsonPointer)
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