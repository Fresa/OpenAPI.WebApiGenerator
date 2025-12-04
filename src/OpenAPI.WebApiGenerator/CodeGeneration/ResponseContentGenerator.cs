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
    private readonly string _responseClassName;

    private ResponseContentGenerator(string statusCodePattern)
    {
        _statusCodePattern = statusCodePattern;
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
        List<ResponseHeaderGenerator> headerGenerators) : this(statusCodePattern)
    {
        _contentGenerators = contentGenerators;
        _headerGenerators = headerGenerators;
    }
    
    public string GenerateResponseContentClass()
    {
        return 
            $$"""
            internal sealed class {{_responseClassName}} : Response
            {
                {{_contentGenerators.AggregateToString(generator =>
                    generator.GenerateConstructor(_responseClassName))}}
                
                {{_contentGenerators.AggregateToString(generator => 
                    generator.GenerateContentProperty())}}
                
                internal override void WriteTo(HttpResponse httpResponse)
                {
                    IJsonValue content = true switch
                    { 
                    {{_contentGenerators.AggregateToString(generator => 
                        $"_ when {generator.ContentPropertyName} is not null => {generator.ContentPropertyName}")}}!,
                        _ => throw new InvalidOperationException("No content was defined") 
                    };
                    
                    using var jsonWriter = new Utf8JsonWriter(httpResponse.BodyWriter);
                    content.WriteTo(jsonWriter);
                }
            }
            """;
    }
}
