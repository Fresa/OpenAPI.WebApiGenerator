namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpResponseExtensionsGenerator(
    string @namespace,
    JsonValueValidationExtensionsGenerator jsonValueValidationExtensionsGenerator)
{
    private const string HttpResponseExtensionsClassName = "HttpResponseExtensions";
    public string Namespace => @namespace;

    internal string CreateWriteHeaderInvocation(
        string responseVariableName, 
        string headerSpecificationAsJson,
        string headerName,
        string headerValueVariableName,
        bool isRequired)
    {
        return
            $""""
            {responseVariableName}.WriteResponseHeader(
            """
            {headerSpecificationAsJson}
            """,
            "{headerName}",
            {headerValueVariableName},
            {isRequired.ToString().ToLowerInvariant()}
            )
            """";
    }
    
    internal string CreateWriteBodyInvocation(
        string responseVariableName, 
        string contentVariableName)
    {
        return
            $"""
             {responseVariableName}.WriteResponseBody(
                {contentVariableName})
             """;
    }
    
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
        using OpenAPI.ParameterStyleParsers.OpenApi20;
        using OpenAPI.ParameterStyleParsers.OpenApi20.ParameterParsers;
        using JsonObject = System.Text.Json.Nodes.JsonObject;
        
        namespace {{{@namespace}}};

        internal static class {{{HttpResponseExtensionsClassName}}}
        {
            private static readonly ConcurrentDictionary<Parameter, ParameterValueParser> ParserCache = new();
        
            internal static void WriteResponseHeader<TValue>(this HttpResponse response, 
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
                {{{jsonValueValidationExtensionsGenerator
                    .CreateValidateInvocation(
                        "value", 
                        "isRequired")}}};
        
                var parameter = Parameter.FromOpenApi20ParameterSpecification(headerSpecificationAsJson);
                var serializedValue = Serialize(parameter, name, value);
                response.Headers[name] = serializedValue;
            }
        
            internal static void WriteResponseBody<TValue>(this HttpResponse response, TValue value)
                where TValue : struct, IJsonValue<TValue>
            {
                var isRequired = true;
                {{{jsonValueValidationExtensionsGenerator
                    .CreateValidateInvocation(
                        "value", 
                        "isRequired")}}};
                using var jsonWriter = new Utf8JsonWriter(response.BodyWriter);
                value.WriteTo(jsonWriter);
            }
            
            private static string? Serialize<TValue>(Parameter parameter, string name, TValue jsonValue)
                where TValue : struct, IJsonValue
            {
                var parser = ParserCache.GetOrAdd(parameter, ParameterValueParser.Create);
                var value = jsonValue.Serialize();
        
                return parser.Serialize(JsonNode.Parse(value));
            }
        }
        #nullable restore
        """");
}