using System.IO;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal sealed class OpenApiStream(OpenApiFileFormat format) : MemoryStream
{
    public OpenApiFileFormat Format { get; } = format;
}