using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseContentGenerator
{
    private readonly List<ResponseBodyContentGenerator> _contentGenerators = [];
    private readonly List<ResponseHeaderGenerator> _headerGenerators = [];
    private readonly HttpResponseExtensionsGenerator _httpResponseExtensionsGenerator;
    private readonly string _responseClassName;
    private readonly string _responseStatusCodePattern;

    private ResponseContentGenerator(string statusCodePattern,
        HttpResponseExtensionsGenerator httpResponseExtensionsGenerator)
    {
        _httpResponseExtensionsGenerator = httpResponseExtensionsGenerator;
        _responseStatusCodePattern = statusCodePattern;
        var classNamePrefix = Enum.TryParse<HttpStatusCode>(statusCodePattern, out var statusCode)
            ? statusCode.ToString()
            : statusCodePattern.First() switch
            {
                '1' => "Informational",
                '2' => "Successful",
                '3' => "Redirection",
                '4' => "ClientError",
                '5' => "ServerError",
                var chr when char.IsDigit(chr) => "X",
                _ => string.Empty
            };
        _responseClassName = $"{classNamePrefix}{statusCodePattern}";
    }
    public ResponseContentGenerator(
        string statusCodePattern,
        List<ResponseBodyContentGenerator> contentGenerators,
        List<ResponseHeaderGenerator> headerGenerators,
        HttpResponseExtensionsGenerator httpResponseExtensionsGenerator) : this(statusCodePattern, httpResponseExtensionsGenerator)
    {
        _contentGenerators = contentGenerators;
        _headerGenerators = headerGenerators;
    }
    
    public string GenerateResponseContentClass()
    {
        var anyHeaders = _headerGenerators.Any();
        var anyRequiredHeader = _headerGenerators.Any(generator => generator.IsRequired);
        var headerRequiredDirective = anyRequiredHeader ? "required" : "";
        var defaultHeadersValueAssignment = anyRequiredHeader ? "" : " = new();";
        const string responseVariableName = "httpResponse";
        const string contentTypeFieldName = "_contentType";
        
        var hasExplicitStatusCode = int.TryParse(_responseStatusCodePattern, out _);
        var hasDefaultStatusCode = _responseStatusCodePattern == "default";
        var needsStatusCodeValidation = !hasExplicitStatusCode && !hasDefaultStatusCode;

        return 
            $$"""
            internal sealed class {{_responseClassName}} : Response
            {
                private string {{contentTypeFieldName}} = string.Empty;
                {{_contentGenerators.AggregateToString(generator =>
                    generator.GenerateConstructor(_responseClassName, contentTypeFieldName))}}
                
                {{_contentGenerators.AggregateToString(generator => 
                    generator.GenerateContentProperty())}}
                
                private int _statusCode{{(hasExplicitStatusCode ? $" = {_responseStatusCodePattern}" : string.Empty)}}; 
                internal int StatusCode
                { 
                    get => _statusCode;{{(hasExplicitStatusCode ? "" : 
                    $"init => _statusCode = {(needsStatusCodeValidation ? $"Validate{_responseStatusCodePattern.First()}xxStatusCode(value)" : "value")};")}}
                }
                
                {{(anyHeaders ? 
                $$"""
                internal {{headerRequiredDirective}} ResponseHeaders Headers { get; init; }{{defaultHeadersValueAssignment}}
                
                internal sealed class ResponseHeaders 
                {
                    {{_headerGenerators.AggregateToString(generator =>
                        generator.GenerateProperty())}}
                }
                """ : "")}}
                
                internal override void WriteTo(HttpResponse {{responseVariableName}})
                {
                    switch (true)
                    { 
                    {{_contentGenerators.AggregateToString(generator => 
                        $"""
                         case true when {generator.ContentPropertyName} is not null:
                            {_httpResponseExtensionsGenerator.CreateWriteBodyInvocation(
                                responseVariableName, 
                                $"{generator.ContentPropertyName}.Value")};
                            break;
                         """
                    )}}
                        default:
                            throw new InvalidOperationException("No content was defined");         
                    }
                    
                    {{responseVariableName}}.ContentType = {{contentTypeFieldName}};
                    {{responseVariableName}}.StatusCode = StatusCode;
                    {{_headerGenerators.AggregateToString(generator =>
                        generator.GenerateWriteDirective(responseVariableName))}}
                }
            }
            """;
    }
}
