using System.IO;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class OpenApiSchemaExtensions
{
    internal static string SerializeToJson(this IOpenApiSchema? schema)
    {
        if (schema is null)
            return "{}";
        
        using var schemaWriter = new StringWriter();
        var openApiSchemaWriter = new OpenApiJsonWriter(schemaWriter, new OpenApiWriterSettings
        {
            InlineLocalReferences = true
        });
        schema.SerializeAsV2(openApiSchemaWriter);
        return schemaWriter.ToString();
    }
}