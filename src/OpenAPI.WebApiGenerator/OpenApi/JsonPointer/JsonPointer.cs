using System;
using System.Linq;
using System.Text;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal readonly struct JsonPointer(params string[]? segments) : IEquatable<JsonPointer>
{
    private string[] Segments => segments ?? [];

    internal JsonPointer Append(string segment)
    {
        return new JsonPointer(Segments.Append(segment).ToArray());
    }

    public override string ToString() => 
        Segments
            .Aggregate(new StringBuilder("#"), (builder, s) => 
                builder.Append($"/{Encode(s)}"))
            .ToString();

    private static string Encode(string segment) => 
        segment.Replace("~", "~0").Replace("/", "~1");

    private readonly int _hashCode = GenerateHashCode(segments ?? []);

    private static int GenerateHashCode(string[] segments)
    {
        var hashCode = new HashCode();
        foreach (var value in segments)
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }
    public override int GetHashCode() => _hashCode;

    public bool Equals(JsonPointer other)
    {
        return _hashCode == other._hashCode;
    }

    public override bool Equals(object? obj)
    {
        return obj is JsonPointer other && Equals(other);
    }
} 