using System.Linq;
using System.Text;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal readonly struct JsonPointer(params string[]? segments)
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
} 