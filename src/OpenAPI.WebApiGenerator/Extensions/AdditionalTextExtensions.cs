using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Microsoft.CodeAnalysis;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.YamlReader;
using OpenAPI.WebApiGenerator.OpenApi;
using SharpYaml.Serialization;
using Path = System.IO.Path;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class AdditionalTextExtensions
{
    private static string[]? _openApiFileExtensions;
    private static string[] OpenApiFileExtensions => _openApiFileExtensions ??=
        DocumentReaders.Keys.ToArray();

    private static Regex? _openApiFilePattern;
    internal static Regex OpenApiFilePattern => _openApiFilePattern ??= new Regex(
        $@"openapi.*\.({string.Join("|", OpenApiFileExtensions)})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    internal static bool IsOpenApiFile(this AdditionalText text) => 
        OpenApiFilePattern.IsMatch(text.Path);

    internal static bool IsOptionsFile(this AdditionalText text) =>
        text.Path.EndsWith("OpenAPI.WebApiGenerator.json");
    
    private static MemoryStream AsStream(this AdditionalText text)
    {
        var content = text.GetText();
        var stream = new MemoryStream();
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

    private static readonly OpenApiJsonReader JsonDocumentReader = new();
    private static readonly OpenApiYamlReader YamlDocumentReader = new();
    private static readonly Dictionary<string, IOpenApiReader> DocumentReaders = new()
    {
        { "json", JsonDocumentReader },
        { "yaml", YamlDocumentReader },
        { "yml", YamlDocumentReader }
    };

    internal static OpenApiSpecification LoadOpenApiSpecification(this AdditionalText text)
    {
        var format = Path.GetExtension(text.Path)
            .TrimStart('.')
            .ToLowerInvariant();
        var stream = text.AsStream();

        var (document, version) = stream.LoadOpenApiDocument(format);
        var openApiUri = new JsonReference(document.BaseUri.ToString());
        var jsonDocument = stream.ParseJsonDocument(format);

        return new OpenApiSpecification(document, version, openApiUri, jsonDocument);
    }

    private static (OpenApiDocument, OpenApiSpecVersion) LoadOpenApiDocument(this MemoryStream stream, string format)
    {
        var openApiResult = OpenApiDocument.Load(
            stream,
            format,
            new OpenApiReaderSettings
            {
                Readers = DocumentReaders,
                LeaveStreamOpen = true
            });
        var version = openApiResult.Diagnostic?.SpecificationVersion ??
                      throw new InvalidOperationException("Unknown openapi version");
        if (openApiResult.Diagnostic.Errors.Any())
        {
            throw new InvalidOperationException(
                openApiResult.Diagnostic.Errors.AggregateToString(
                    "Errors while parsing OpenAPI specification: ",
                    error => $"{(error.Pointer == null ? "" : $"{error.Pointer}: ")}{error.Message}"));
        }
        var document = openApiResult.Document ??
                       throw new InvalidOperationException(
                           "OpenAPI document is empty");
        return (document, version);
    }
    
    private static JsonDocument ParseJsonDocument(this MemoryStream stream, string format)
    {
        stream.Position = 0;
        return format switch
        {
            "json" => JsonDocument.Parse(stream),
            "yaml" or "yml" => GetFromYaml(),
            _ => throw new InvalidOperationException($"Format {format} not supported")
        };

        JsonDocument GetFromYaml()
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StreamReader(stream));
            return JsonDocument.Parse(yamlStream.First().ToJsonNode().ToJsonString());
        }
    }
}