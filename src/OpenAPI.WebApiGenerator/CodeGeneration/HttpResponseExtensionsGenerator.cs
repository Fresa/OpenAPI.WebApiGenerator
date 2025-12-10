namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpResponseExtensionsGenerator(string @namespace)
{
    private const string HttpResponseExtensionsClassName = "HttpResponseExtensions";
    public string Namespace => @namespace;

    internal string CreateWriteHeaderInvocation(
        string responseVariableName, 
        string bindingTypeName,
        string headerSpecificationAsJson,
        string headerName,
        string headerValueVariableName)
    {
        return
            $""""
            {responseVariableName}.WriteResponseHeader<{bindingTypeName}>(
            """
            {headerSpecificationAsJson}
            """,
            "{headerName}",
            {headerValueVariableName}
            )
            """";
    }
    
    internal string CreateWriteBodyInvocation(
        string responseVariableName, 
        string headerValueVariableName)
    {
        return
            $"""
             {responseVariableName}.WriteResponseBody(
                {headerValueVariableName})
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
                TValue value)
                where TValue : struct, IJsonValue
            {
                Validate(value);
                var parameter = Parameter.FromOpenApi20ParameterSpecification(headerSpecificationAsJson);
                var serializedValue = Serialize(parameter, name, value);
                response.Headers[name] = serializedValue;
            }
        
            internal static void WriteResponseBody(this HttpResponse response, IJsonValue value)
            {
                Validate(value);
                using var jsonWriter = new Utf8JsonWriter(response.BodyWriter);
                value.WriteTo(jsonWriter);
            }
            
            private static void Validate(IJsonValue value)
            {
                var validationContext = ValidationContext.ValidContext;
                value.Validate(validationContext);
                if (validationContext.IsValid)
                {
                    return;
                }
        
                var validationResults = validationContext.Results.IsEmpty ? "None" : JsonSerializer.Serialize(validationContext.Results, new JsonSerializerOptions { WriteIndented = true });
                throw new InvalidOperationException(
                    $"""
                     Object of type {value.GetType()} is not valid'.
                     "Validation results: {validationResults}
                     """);
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