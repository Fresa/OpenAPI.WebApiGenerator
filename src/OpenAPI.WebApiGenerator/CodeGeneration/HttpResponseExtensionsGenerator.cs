namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpResponseExtensionsGenerator(
    string @namespace)
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

        internal static class {{{HttpResponseExtensionsClassName}}}
        {
            private static readonly ConcurrentDictionary<string, IParameterValueParser> ParserCache = new();
            
            internal static void WriteResponseHeader<TValue>(this HttpResponse response,
                string openApiVersion, 
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
                    _ => ParameterValueParserFactory.OpenApi(openApiVersion, headerSpecificationAsJson));        
                var jsonValue = value.Serialize();
                var serializedValue = parser.Serialize(JsonNode.Parse(jsonValue));
                response.Headers[name] = serializedValue;
            }
        
            internal static void WriteResponseBody<TValue>(this HttpResponse response, TValue value)
                where TValue : struct, IJsonValue<TValue>
            {
                using var jsonWriter = new Utf8JsonWriter(response.BodyWriter);
                value.WriteTo(jsonWriter);
            }
        }
        #nullable restore
        """");
}