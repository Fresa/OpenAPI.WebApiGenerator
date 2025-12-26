using System;
using System.Collections.Generic;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal abstract class OpenApiVisitor
{
    public static IOpenApiVisitor V3(OpenApiReference<OpenApiDocument> openApiReference) => 
        OpenApiV3Visitor.Visit(openApiReference);
    public static IOpenApiVisitor V2(OpenApiReference<OpenApiDocument> openApiReference) => 
        OpenApiV2Visitor.Visit(openApiReference);
}

internal abstract class OpenApiVisitor<T>(
    OpenApiReference<T> openApiReference)
{
    internal JsonReference Reference => openApiReference.DocumentReference; 
    private JsonPointer Pointer { get; } = JsonPointer.ParseFrom(openApiReference.DocumentReference);
    protected JsonDocument Document => openApiReference.OpenApiDocument;
    protected T OpenApiDocument => openApiReference.Document;
    
    protected JsonPointer Visit(params string[] segments) =>
        TryVisit(segments, out var jsonPointer)
            ? jsonPointer
            : throw new InvalidOperationException($"{jsonPointer} doesn't exist in openapi document");

    private readonly HashSet<JsonPointer> _cache = [];
    protected bool TryVisit(string[] segments, out JsonPointer jsonPointer)
    {
        jsonPointer = Pointer;
        foreach (var segment in segments)
        {
            jsonPointer = jsonPointer.Append(segment);
            if (_cache.Contains(jsonPointer))
            {
                continue;
            }

            if (!JsonPointerUtilities.TryResolvePointer(Document, jsonPointer.ToString().AsSpan(), out var node))
            {
                return false;
            }

            if (JsonPointerUtilities.TryResolvePointer(node.Value, "#/$ref".AsSpan(), out var refNode))
            {
                jsonPointer = refNode.Value.ValueKind switch
                {
                    JsonValueKind.String => JsonPointer.ParseFrom(refNode.Value.GetString()!),
                    _ => jsonPointer
                };
            }

            _cache.Add(jsonPointer);
        }
        
        return true;            
    }
}