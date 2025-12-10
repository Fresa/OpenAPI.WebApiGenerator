using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseContentGenerator
{
    private readonly string _statusCodePattern;
    private readonly List<ResponseBodyContentGenerator> _contentGenerators = [];
    private readonly List<ResponseHeaderGenerator> _headerGenerators = [];
    private readonly HttpResponseExtensionsGenerator _httpResponseExtensionsGenerator;
    private readonly string _responseClassName;

    private ResponseContentGenerator(string statusCodePattern,
        HttpResponseExtensionsGenerator httpResponseExtensionsGenerator)
    {
        _statusCodePattern = statusCodePattern;
        _httpResponseExtensionsGenerator = httpResponseExtensionsGenerator;
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
        var responseVariableName = "httpResponse";
        return 
            $$"""
            internal sealed class {{_responseClassName}} : Response
            {
                {{_contentGenerators.AggregateToString(generator =>
                    generator.GenerateConstructor(_responseClassName))}}
                
                {{_contentGenerators.AggregateToString(generator => 
                    generator.GenerateContentProperty())}}
                
                {{(anyHeaders ? 
                $$"""
                internal {{headerRequiredDirective}} ResponseHeaders Headers { get; init; } {{(anyRequiredHeader ? "= new()" : "")}}
                
                internal sealed class ResponseHeaders 
                {
                    {{_headerGenerators.AggregateToString(generator =>
                        generator.GenerateProperty())}}
                }
                """ : "")}}
                
                internal override void WriteTo(HttpResponse {{responseVariableName}})
                {
                    IJsonValue content = true switch
                    { 
                    {{_contentGenerators.AggregateToString(generator => 
                        $"_ when {generator.ContentPropertyName} is not null => {generator.ContentPropertyName}")}}!,
                        _ => throw new InvalidOperationException("No content was defined") 
                    };
                    
                    {{_httpResponseExtensionsGenerator.CreateWriteBodyInvocation(responseVariableName, "content")}};
                    {{_headerGenerators.AggregateToString(generator =>
                        generator.GenerateWriteDirective(responseVariableName))}}
                }
            }
            """;
    }
}
