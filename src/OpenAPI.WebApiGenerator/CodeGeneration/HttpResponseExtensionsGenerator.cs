using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpResponseExtensionsGenerator(
    OpenApiSpecVersion openApiVersion,
    string @namespace)
{
    private const string HttpResponseExtensionsClassName = "HttpResponseExtensions";
    public string Namespace => @namespace;
    
    internal string GetResponseHeaderSpecificationAsJson(
        IOpenApiHeader header, 
        string name) =>
        // Response header specification is a subset of the parameter specification, so we add the missing properties to be able to use the parameter value parser 
        $$"""
          {
            "name": "{{name}}",
            "in": "header",
            {{header.Serialize(openApiVersion).ToString().TrimStart('{').TrimStart()}} 
          """;

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
            private static readonly ConcurrentDictionary<IParameter, IParameterValueParser> ParserCache = new();
            private static IParameterValueParser GetParser(IParameter parameter) => ParserCache.GetOrAdd(parameter, _ => parameter.CreateParameterValueParser());
            
            internal static void WriteResponseHeader<TValue>(this HttpResponse response,
                string openApiVersion, 
                string headerSpecificationAsJson, 
                string name, 
                TValue value,
                bool isRequired)
                where TValue : struct, IJsonValue
            {
                if (!isRequired && value.IsUndefined()) 
                {
                    return;
                }
                
                Validate(value);
        
                var parameter = ParameterFactory.OpenApi(openApiVersion, headerSpecificationAsJson);
                var serializedValue = Serialize(parameter, name, value);
                response.Headers[name] = serializedValue;
            }
        
            internal static void WriteResponseBody<TValue>(this HttpResponse response, TValue value)
                where TValue : struct, IJsonValue<TValue>
            {
                Validate(value);
                
                using var jsonWriter = new Utf8JsonWriter(response.BodyWriter);
                value.WriteTo(jsonWriter);
            }
            
            private static string? Serialize<TValue>(IParameter parameter, string name, TValue jsonValue)
                where TValue : struct, IJsonValue
            {
                var parser = GetParser(parameter);
                var value = jsonValue.Serialize();
        
                return parser.Serialize(JsonNode.Parse(value));
            }
            
            private static void Validate<T>(T value)
                where T : struct, IJsonValue
            {
                var validationContext = ValidationContext.ValidContext;
                var validationLevel = ValidationLevel.Detailed;
                validationContext = value.Validate(validationContext, validationLevel);
                if (!validationContext.IsValid)
                {
                    throw new JsonValidationException($"Object of type {typeof(T)} is not valid", validationContext.Results);
                }
            }
        }
        #nullable restore
        """");
}