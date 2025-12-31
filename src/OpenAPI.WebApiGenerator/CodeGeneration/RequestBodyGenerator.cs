using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        _contentGenerators = contentGenerators;
    }

    internal static readonly RequestBodyGenerator Empty = new();
    
    internal string GenerateRequestBindingDirective(string propertyName, string requestVariableName, out bool isAsync)
    {
        isAsync = _body is not null;
        if (_body is null)
        {
            return string.Empty;
        }

        return $"""
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

        return $$"""
                 internal {{(Body.Required ? "required " : "")}}RequestContent{{(Body.Required ? "" : "?")}} {{propertyName}} { get; init; }

                 internal sealed class RequestContent 
                 {
                    {{_contentGenerators.Aggregate(new StringBuilder(), (builder, content) => builder.AppendLine(content.GenerateRequestProperty()))}}
                    
                    internal static async Task<RequestContent{{(_body.Required ? "" : "?")}}> BindAsync(
                        HttpRequest request,
                        CancellationToken cancellationToken)
                    {
                        var requestContentType = request.ContentType;
                        var requestContentMediaType = requestContentType == null ? null : System.Net.Http.Headers.MediaTypeHeaderValue.Parse(requestContentType);
                 
                        switch (requestContentMediaType?.MediaType?.ToLower()) 
                        {
                            {{_contentGenerators.Aggregate(new StringBuilder(), (builder, content) => builder.AppendLine(
                                $$"""
                                  case "{{content.ContentType.ToLower()}}":
                                      return new RequestContent
                                      {
                                          {{content.GenerateRequestBindingDirective(_body.Required)}}
                                      };
                                  """
                            ))}}
                            {{(_body.Required ? "" :
                                """
                                case "":
                                    return null;
                                """)}}
                                default:
                                    throw new BadHttpRequestException($"Request body does not support content type {requestContentType}");
                        }
                    }
                    
                    internal ValidationContext Validate(ValidationContext validationContext, ValidationLevel validationLevel)
                    {
                        switch (true) 
                        {
                            {{_contentGenerators.AggregateToString(content => 
                                       $"""
                                        case true when {content.PropertyName} is not null:
                                            return {content.PropertyName}!.Value.Validate(validationContext, validationLevel);
                                        """)}}
                            default:
                            {{(_body.Required ? 
                                """throw new InvalidOperationException("Request body not set");""" :
                                "return validationContext;")}}
                        }
                    }
                 }
                 """;
    }
}