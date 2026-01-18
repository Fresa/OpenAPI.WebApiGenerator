using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Corvus.Json;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.YamlReader;
using OpenAPI.WebApiGenerator.OpenApi;
using SharpYaml.Serialization;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class StreamExtensions
{
    private static JsonDocument LoadJsonDocument(this OpenApiStream stream)
    {
        stream.Position = 0;
        return stream.Format switch
        {
            OpenApiFileFormat.Json => JsonDocument.Parse(stream),
            OpenApiFileFormat.Yml or OpenApiFileFormat.Yaml => GetFromYaml(),
            _ => throw new ArgumentOutOfRangeException(nameof(stream.Format), stream.Format, "Supported formats are json, yml and yaml")
        };

        JsonDocument GetFromYaml()
        {
            var yamlStream = new YamlStream();
            yamlStream.Load(new StreamReader(stream));
            return JsonDocument.Parse(yamlStream.First().ToJsonNode().ToJsonString());
        }
    }
    
    private static readonly OpenApiJsonReader JsonDocumentReader = new();
    private static readonly OpenApiYamlReader YamlDocumentReader = new();
    private static readonly Dictionary<string, IOpenApiReader> DocumentReaders = new()
    {
        { "json", JsonDocumentReader },
        { "yaml", YamlDocumentReader },
        { "yml", YamlDocumentReader }
    };

    internal static OpenApiSpecification LoadOpenApiDocument(this OpenApiStream stream)
    {
        stream.Position = 0;
        var openApiResult = OpenApiDocument.Load(
            stream,
            Enum.GetName(typeof(OpenApiFileFormat), stream.Format)?.ToLowerInvariant(),
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
        var openApiUri = new JsonReference(document.BaseUri.ToString());
        var jsonDocument = stream.LoadJsonDocument();
        return new OpenApiSpecification(document, version, openApiUri, jsonDocument);
    }
}