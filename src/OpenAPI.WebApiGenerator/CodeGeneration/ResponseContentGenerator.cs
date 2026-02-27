using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseContentGenerator
{
    private readonly List<ResponseBodyContentGenerator> _contentGenerators = [];
    private readonly List<ResponseHeaderGenerator> _headerGenerators = [];
    private readonly string _responseClassName;
    private readonly string _responseStatusCodePattern;
    private readonly IOpenApiResponse _response;

    private ResponseContentGenerator(
        KeyValuePair<string, IOpenApiResponse> response)
    {
        var responseStatusCodePattern = response.Key.ToPascalCase();
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
        _response = response.Value;
    }
    public ResponseContentGenerator(
        KeyValuePair<string, IOpenApiResponse> response,
        List<ResponseBodyContentGenerator> contentGenerators,
        List<ResponseHeaderGenerator> headerGenerators) : this(response)
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
{{{_response.Description.AsComment("summary", "para")}}}
internal {{{(_contentGenerators.Any() ? "abstract" : "sealed")}}} class {{{_responseClassName}}} : Response{{{(_contentGenerators.Any() ? $", IContent<{_responseClassName}>" : "")}}}
{
    private string? {{{contentTypeFieldName}}} = null;{{{
    _contentGenerators.AggregateToString(generator =>
        generator.GenerateResponseClass(_responseClassName, contentTypeFieldName)).Indent(4)
    }}}{{{(_contentGenerators.Any() ? 
$$"""


    protected abstract IJsonValue Content { get; }
    protected abstract string ContentSchemaLocation { get; }
      
    /// <inheritdoc/>
    public static ContentMediaType<{{_responseClassName}}>[] ContentMediaTypes { get; } = 
    [{{_contentGenerators.AggregateToString(generator => 
$$"""
        {{generator.ClassName}}.ContentMediaType,
""").TrimEnd(',')}}
    ];
""" : "")}}}
    
    private int _statusCode{{{(hasExplicitStatusCode ? $" = {_responseStatusCodePattern}" : string.Empty)}}};
    /// <summary>
    /// Response status code
    /// </summary> 
    internal int StatusCode
    { 
        get => _statusCode;{{{(hasExplicitStatusCode ? "" : 
$"""
        init => _statusCode = {(needsStatusCodeValidation ? $"Validate{_responseStatusCodePattern.First()}xxStatusCode(value)" : "value")};
""")}}}
    }
{{{(anyHeaders ? 
$$"""

    /// <summary>
    /// Response Headers
    /// </summary> 
    internal {{headerRequiredDirective}}ResponseHeaders Headers { get; init; }{{defaultHeadersValueAssignment}}

    /// <summary>
    /// Response Headers
    /// </summary> 
    internal sealed class ResponseHeaders 
    {{{
        _headerGenerators.AggregateToString(generator =>
            generator.GenerateProperty()).Indent(8)}}
    }

""" : "")}}}
    /// <inheritdoc/>
    internal override void WriteTo(HttpResponse {{{responseVariableName}}})
    {{{{(_contentGenerators.Any() ? 
$$"""

        {{HttpResponseExtensionsGenerator.CreateWriteBodyInvocation(
            responseVariableName, 
            "Content")}};
""" : "")}}}
        {{{responseVariableName}}}.ContentType = {{{contentTypeFieldName}}};
        {{{responseVariableName}}}.StatusCode = StatusCode;{{{
        _headerGenerators.AggregateToString(generator =>
            generator.GenerateWriteDirective(responseVariableName)).Indent(8)}}}
    }
    
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = ValidationContext.ValidContext.UsingStack().UsingResults();
        {{{(_contentGenerators.Any() ? 
$"""
        validationContext = Content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
""" : "")}}}
        {{{_headerGenerators.AggregateToString(generator =>
            generator.GenerateValidateDirective()).Indent(8)}}}
        return validationContext;
    }
}
""".Trim();
    }
}
