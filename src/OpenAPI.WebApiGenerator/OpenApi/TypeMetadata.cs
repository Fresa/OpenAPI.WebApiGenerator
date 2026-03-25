using System;
using System.Linq;
using OpenAPI.WebApiGenerator.Extensions;
using OpenAPI.WebApiGenerator.Json;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal sealed class TypeMetadata(string @namespace, string path, string name)
{
    private static readonly string[] SchemaMetaLeafNodeNames = ["schema", "itemSchema"];
    internal static TypeMetadata From(JsonPointer pointer)
    {
        var segments = 
            pointer.Segments
                // normalize segment names
                .Select(segment =>
                    segment.ToPascalCase())
                // namespace segments cannot start with an integer
                .Select(segment =>
                    int.TryParse(segment[..1], out _) ? $"_{segment}" : segment)
                .ToArray()
                .AsSpan();
        // Remove any schema leaf node, as that is metadata and doesn't describe the name of the type
        if (SchemaMetaLeafNodeNames.Contains(segments[^1], StringComparer.OrdinalIgnoreCase))
        {
            segments = segments[..^1];
        }
        var path = System.IO.Path.Combine(segments.ToArray());

        // Last segment is the type name
        var name = segments[^1];
        segments = segments[..^1];
        var @namespace = string.Join(".", segments.ToArray());

        return new TypeMetadata(@namespace, path, name);
    }

    public string Namespace => @namespace;
    public string Name => name;
    public string Path => path;
}