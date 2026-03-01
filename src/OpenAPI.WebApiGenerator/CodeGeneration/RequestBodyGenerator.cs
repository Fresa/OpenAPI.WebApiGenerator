using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class RequestBodyGenerator
{
    private readonly IOpenApiRequestBody? _body;
    private readonly List<RequestBodyContentGenerator> _contentGenerators = [];

    private IOpenApiRequestBody Body =>
        _body ?? throw new NullReferenceException(nameof(_body));

    
    private RequestBodyGenerator()
    {
        
    }
    public RequestBodyGenerator(
        IOpenApiRequestBody? body,
        List<RequestBodyContentGenerator> contentGenerators)
    {
        _body = body;
        _contentGenerators = contentGenerators
            .SortByContentType()
            .ToList();
    }

    internal static readonly RequestBodyGenerator Empty = new();
    
    internal string GenerateRequestBindingDirective(string propertyName, string requestVariableName, out bool isAsync)
    {
        isAsync = _body is not null;
        return _body is null
            ? string.Empty
            : $"""
               {propertyName} = await RequestContent.BindAsync({requestVariableName}, cancellationToken)
                   .ConfigureAwait(false)
               """;
    }
    
    internal string GenerateValidateDirective(string propertyName, string validationContextVariableName, string validationLevelVariableName)
    {
        if (_body is null)
        {
            return string.Empty;
        }

        return $"""
                
                {validationContextVariableName} = {propertyName}{(Body.Required ? "" : "?")}.Validate(
                    {validationContextVariableName}, 
                    {validationLevelVariableName}){(Body.Required ? "" : $" ?? {validationContextVariableName}")};
                """;
    }

    public string GenerateRequestProperty(string propertyName)
    {
        if (_body is null)
        {
            return string.Empty;
        }

        return 
$$"""
/// <summary>
/// Request content
/// </summary>
internal {{(Body.Required ? "required " : "")}}RequestContent{{(Body.Required ? "" : "?")}} {{propertyName}} { get; init; }

/// <summary>
/// Request content
/// </summary>
internal sealed class RequestContent(string? requestContentType, bool invalidContentType = false)
{{{
    _contentGenerators.AggregateToString(content => 
        content.GenerateRequestProperty()).Indent(4)}}
    /// <summary>
    /// Bind request content from http request
    /// </summary>
    /// <param name="request">Http request to bind from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An awaitable task for the request content</returns>
    internal static async Task<RequestContent{{(_body.Required ? "" : "?")}}> BindAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var requestContentType = request.ContentType;
        var requestContentMediaType = requestContentType == null ? null : System.Net.Http.Headers.MediaTypeHeaderValue.Parse(requestContentType);

        switch (requestContentMediaType?.MediaType) 
        {{{_contentGenerators.AggregateToString(content => 
$$"""
            case not null when {{content.ContentType.GetMatchConditionExpression("requestContentMediaType")}}:
                return new RequestContent(requestContentType)
                {
{{content.GenerateRequestBindingDirective().Indent(20)}}
                };
""")}}{{(_body.Required ? "" :
"""
            case null:
                return null;
""")}}
            default:
                return new RequestContent(requestContentType, true);
        }
    }

    /// <summary>
    /// Validate the request content
    /// </summary>
    /// <param name="validationContext">Current validation context</param>
    /// <param name="validationLevel">Validation level</param>
    /// <returns>The validation result</returns>
    internal ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel) =>
        true switch
        {{{_contentGenerators.AggregateToString(content => 
$"""
            true when {content.PropertyName} is not null =>
                {content.PropertyName}!.Value.Validate("{content.SchemaLocation}", true, validationContext, validationLevel),
""")}} 
            true when requestContentType is null =>
                {{(_body.Required ? """validationContext.WithResult(false, "Request content is missing")""" : "validationContext")}},
            true when invalidContentType =>
                validationContext.WithResult(false, $"Request content type {requestContentType} is not supported"),
            _ => validationContext
        };
}
""";
    }
}