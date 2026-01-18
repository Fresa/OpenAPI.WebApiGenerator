using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using OpenAPI.WebApiGenerator.OpenApi;
using Path = System.IO.Path;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class AdditionalTextExtensions
{
    internal static readonly string[] OpenApiFileExtensions = 
        Enum.GetNames(typeof(OpenApiFileFormat))
            .Select(openApiFileFormat => 
                $".{openApiFileFormat.ToLowerInvariant()}")
            .ToArray();  
    
    internal static bool IsOpenApiFileFormat(this AdditionalText text)
    {
        var extension = text.GetExtension();
        return OpenApiFileExtensions.Contains(extension);
    }
    
    private static OpenApiFileFormat GetOpenApiFileFormat(this AdditionalText text)
    {
        var format = text.GetExtension().TrimStart('.');
        if (Enum.TryParse<OpenApiFileFormat>(format, true, out var openApiFileFormat))
        {
            return openApiFileFormat;
        }

        throw new InvalidOperationException(
            $"{text.Path} is not a recognized OpenAPI file format. Expected one of {string.Join(", ", Enum.GetNames(typeof(OpenApiFileFormat)))}");
    }
    
    internal static OpenApiStream AsOpenApiStream(this AdditionalText text)
    {
        var content = text.GetText();
        var format = text.GetOpenApiFileFormat();
        var stream = new OpenApiStream(format);
        if (content is null)
        {
            return stream;
        }

        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
        {
            content.Write(writer);    
        }
        
        stream.Position = 0;
        return stream;
    }

    private static string GetExtension(this AdditionalText text) => 
        Path.GetExtension(text.Path);
}