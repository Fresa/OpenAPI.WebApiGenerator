using System;
using System.Collections.Generic;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.OpenApi.Visitor.V2;
using OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;
using JsonPointer = OpenAPI.WebApiGenerator.Json.JsonPointer;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal abstract class OpenApiVisitor
{
    public static IOpenApiVisitor V(OpenApiSpecVersion version, OpenApiReference<OpenApiDocument> openApiReference) =>
        version switch
        {
            OpenApiSpecVersion.OpenApi2_0 => OpenApiV2Visitor.Visit(openApiReference),
            OpenApiSpecVersion.OpenApi3_0 or OpenApiSpecVersion.OpenApi3_1 => 
                OpenApiV3Visitor.Visit(openApiReference),
            _ => throw new InvalidOperationException($"OpenAPI version {version} not supported")
        };
}

internal abstract class OpenApiVisitor<T>(
    OpenApiReference<T> openApiReference) : IVisitor
{
    internal JsonReference Reference => openApiReference.DocumentReference; 
    public JsonPointer Pointer { get; } = JsonPointer.ParseFrom(openApiReference.DocumentReference);
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