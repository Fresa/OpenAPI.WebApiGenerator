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
    private readonly string _responseClassName;
    private readonly string _responseStatusCodePattern;

    private ResponseContentGenerator(
        string responseStatusCodePattern)
    {
        var classNamePrefix = Enum.TryParse<HttpStatusCode>(responseStatusCodePattern, out var statusCode)
            ? statusCode.ToString()
            : responseStatusCodePattern.First() switch
            {
                '1' => "Informational",
                '2' => "Successful",
                '3' => "Redirection",
                '4' => "ClientError",
                '5' => "ServerError",
                var chr when char.IsDigit(chr) => "X",
                _ => string.Empty
            };
        var responseClassName = $"{classNamePrefix}{responseStatusCodePattern}";
        
        _responseStatusCodePattern = responseStatusCodePattern;
        _responseClassName = responseClassName;
    }
    public ResponseContentGenerator(
        string responseStatusCodePattern,
        List<ResponseBodyContentGenerator> contentGenerators,
        List<ResponseHeaderGenerator> headerGenerators) : this(responseStatusCodePattern)
    {
        _contentGenerators = contentGenerators;
        _headerGenerators = headerGenerators;
    }
    
    public string GenerateResponseContentClass()
    {
        var anyHeaders = _headerGenerators.Any();
        var anyRequiredHeader = _headerGenerators.Any(generator => generator.IsRequired);
        var headerRequiredDirective = anyRequiredHeader ? "required " : "";
        var defaultHeadersValueAssignment = anyRequiredHeader ? "" : " = new();";
        const string responseVariableName = "httpResponse";
        const string contentTypeFieldName = "_contentType";
        
        var hasExplicitStatusCode = int.TryParse(_responseStatusCodePattern, out _);
        var hasDefaultStatusCode = _responseStatusCodePattern == "default";
        var needsStatusCodeValidation = !hasExplicitStatusCode && !hasDefaultStatusCode;

        return 
$$$"""
internal sealed class {{{_responseClassName}}} : Response
{
    private string? {{{contentTypeFieldName}}} = null;{{{
    _contentGenerators.AggregateToString(generator =>
        generator.GenerateConstructor(_responseClassName, contentTypeFieldName)).Indent(4)
    }}}{{{
    _contentGenerators.AggregateToString(generator => 
        generator.GenerateContentProperty()).Indent(4)
    }}}
    
    private int _statusCode{{{(hasExplicitStatusCode ? $" = {_responseStatusCodePattern}" : string.Empty)}}}; 
    internal int StatusCode
    { 
        get => _statusCode;{{{(hasExplicitStatusCode ? "" : 
$"""
        init => _statusCode = {(needsStatusCodeValidation ? $"Validate{_responseStatusCodePattern.First()}xxStatusCode(value)" : "value")};
""")}}}
    }
{{{(anyHeaders ? 
$$"""

    internal {{headerRequiredDirective}}ResponseHeaders Headers { get; init; }{{defaultHeadersValueAssignment}}

    internal sealed class ResponseHeaders 
    {{{
        _headerGenerators.AggregateToString(generator =>
            generator.GenerateProperty()).Indent(8)}}
    }

""" : "")}}}
    internal override void WriteTo(HttpResponse {{{responseVariableName}}})
    {{{{(_contentGenerators.Any() ? 
$$"""

        switch (true)
        {{{_contentGenerators.AggregateToString(generator => 
$"""
            case true when {generator.ContentPropertyName} is not null:
                {HttpResponseExtensionsGenerator.CreateWriteBodyInvocation(
                    responseVariableName, 
                    $"{generator.ContentPropertyName}.Value")};
                break;
""")}}
            default:
                throw new InvalidOperationException("No content was defined");         
        }

""" : "")}}}
        {{{responseVariableName}}}.ContentType = {{{contentTypeFieldName}}};
        {{{responseVariableName}}}.StatusCode = StatusCode;{{{
        _headerGenerators.AggregateToString(generator =>
            generator.GenerateWriteDirective(responseVariableName)).Indent(8)}}}
    }
    
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = ValidationContext.ValidContext.UsingStack().UsingResults();
        validationContext = true switch
        {{{{_contentGenerators.AggregateToString(generator => 
$"""
            true when {generator.ContentPropertyName} is not null =>
                {generator.ContentPropertyName}.Value.Validate("{generator.SchemaLocation}", true, validationContext, validationLevel),
""")}}}
            _ => validationContext          
        };
        {{{_headerGenerators.AggregateToString(generator =>
            generator.GenerateValidateDirective()).Indent(8)}}}
        return validationContext;
    }
}
""";
    }
}
