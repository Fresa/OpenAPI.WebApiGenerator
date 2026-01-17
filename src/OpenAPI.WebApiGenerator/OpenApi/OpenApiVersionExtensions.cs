using System;
using System.IO;
using System.Text;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal static class OpenApiVersionExtensions
{
    internal static string GetParameterVersion(this OpenApiSpecVersion version) => version switch
    {
        OpenApiSpecVersion.OpenApi2_0 => "2.0",
        OpenApiSpecVersion.OpenApi3_0 => "3.0",
        OpenApiSpecVersion.OpenApi3_1 => "3.1",
        OpenApiSpecVersion.OpenApi3_2 => "3.2",
        _ => throw new NotSupportedException($"OpenAPI version {Enum.GetName(typeof(OpenApiSpecVersion), version)} not supported")
    };

    internal static Action<IOpenApiWriter> GetSerializer(this IOpenApiSerializable parameter, OpenApiSpecVersion version) => version switch
    {
        OpenApiSpecVersion.OpenApi3_2 => parameter.SerializeAsV32,
        OpenApiSpecVersion.OpenApi3_1 => parameter.SerializeAsV31,
        OpenApiSpecVersion.OpenApi3_0 => parameter.SerializeAsV3,
        OpenApiSpecVersion.OpenApi2_0 => parameter.SerializeAsV2,
        _ => throw new NotSupportedException(
            $"OpenAPI version {Enum.GetName(typeof(OpenApiSpecVersion), version)} not supported")
    };

    internal static StringBuilder Serialize(this IOpenApiSerializable serializable, OpenApiSpecVersion version)
    {
        using var textWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings
        {
            InlineLocalReferences = true
        });
        var serialize = serializable.GetSerializer(version);
        serialize(jsonWriter);
        textWriter.Flush();
        return textWriter.GetStringBuilder();
    }
}