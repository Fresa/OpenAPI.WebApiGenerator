using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal sealed class OpenApiSpecification(
    OpenApiDocument document,
    OpenApiSpecVersion version,
    JsonReference url,
    JsonDocument jsonDocument)
{
    public OpenApiDocument Document { get; } = document;
    public OpenApiSpecVersion Version { get; } = version;
    public JsonReference Url { get; } = url;
    public JsonDocument JsonDocument { get; } = jsonDocument;
}