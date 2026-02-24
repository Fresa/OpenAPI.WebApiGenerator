using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpResponseExtensionsGenerator(
    string @namespace,
    OpenApiSpecVersion openApiSpecVersion)
{
    private const string HttpResponseExtensionsClassName = "HttpResponseExtensions";
    public string Namespace => @namespace;
    
    internal static string CreateWriteBodyInvocation(
        string responseVariableName, 
        string contentVariableName) =>
        $"""
         {responseVariableName}.WriteResponseBody({contentVariableName})
         """;

    internal SourceCode GenerateHttpResponseExtensionsClass() =>
        new($"{HttpResponseExtensionsClassName}.g.cs",
        $$$""""
        #nullable enable
        using System.Collections.Concurrent;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Corvus.Json;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.Primitives;
        using OpenAPI.ParameterStyleParsers;
        using JsonObject = System.Text.Json.Nodes.JsonObject;
        
        namespace {{{@namespace}}};
        
        /// <summary>
        /// Extension methods for http response objects
        /// </summary>
        internal static class {{{HttpResponseExtensionsClassName}}}
        {
            private static readonly ConcurrentDictionary<string, IParameterValueParser> ParserCache = new();
            private const string ParameterValueParserVersion = "{{{openApiSpecVersion.GetParameterVersion()}}}";
            
            /// <summary>
            /// Write header to a response object
            /// </summary>
            /// <param name="response">The response object to write the header to</param>
            /// <param name="headerSpecificationAsJson">OpenAPI specification for the header</param>
            /// <param name="name">The header name</param>
            /// <param name="value">The header value</param>
            /// <typeparam name="TValue">The type of the header</typeparam>
            internal static void WriteResponseHeader<TValue>(this HttpResponse response,
                string headerSpecificationAsJson, 
                string name, 
                TValue value)
                where TValue : struct, IJsonValue
            {
                if (value.IsUndefined()) 
                {
                    return;
                }

                var parser = ParserCache.GetOrAdd(headerSpecificationAsJson, 
                    _ => ParameterValueParserFactory.OpenApi(ParameterValueParserVersion, headerSpecificationAsJson));        
                var jsonValue = value.Serialize();
                var serializedValue = parser.Serialize(JsonNode.Parse(jsonValue));
                response.Headers[name] = serializedValue;
            }
        
            /// <summary>
            /// Write body to a response object
            /// </summary>
            /// <param name="response">The response object to write the body to</param>
            /// <param name="value">The value of the body</param>
            internal static void WriteResponseBody(this HttpResponse response, IJsonValue value)
            {
                using var jsonWriter = new Utf8JsonWriter(response.BodyWriter);
                value.WriteTo(jsonWriter);
            }
        }
        #nullable restore
        """");
}