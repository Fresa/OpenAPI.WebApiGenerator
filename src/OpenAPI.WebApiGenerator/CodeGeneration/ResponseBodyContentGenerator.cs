using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator
{
    private readonly string _contentVariableName;
    internal string ClassName { get; }
    private readonly MediaTypeHeaderValue _contentType;
    private readonly TypeDeclaration _typeDeclaration;
    private readonly bool _isContentTypeRange;
    private readonly bool _isSequentialMediaType;
    
    public ResponseBodyContentGenerator(KeyValuePair<string, IOpenApiMediaType> contentMediaType, TypeDeclaration typeDeclaration)
    { 
        _contentType = MediaTypeHeaderValue.Parse(contentMediaType.Key);
        _typeDeclaration = typeDeclaration;
        _isSequentialMediaType = contentMediaType.Value.ItemSchema != null;
        _isContentTypeRange = _contentType.MediaType.EndsWith("*");
        _contentVariableName = _contentType.MediaType switch
        {
            "*/*" => "any",
            not null when _isContentTypeRange =>
                $"any{_contentType.MediaType.TrimEnd('*').TrimEnd('/').ToLower().ToPascalCase()}",
            null => throw new InvalidOperationException("Content type is null"),
            _ => _contentType.MediaType.ToLower().ToCamelCase()
        };

        ClassName = _contentVariableName.ToPascalCase();
    }

    private string SchemaLocation => _typeDeclaration.RelativeSchemaLocation;
    public string GenerateResponseClass(string responseClassName, string contentTypeFieldName) =>
        _isSequentialMediaType ? 
$$"""
/// <summary>
/// Response for content {{_contentType}}
/// </summary>
internal sealed class {{ClassName}} : {{responseClassName}}
{
    private readonly {{ClassName}}Writer<{{_typeDeclaration.FullyQualifiedDotnetTypeName()}}> _content;
    private {{_typeDeclaration.FullyQualifiedDotnetTypeName()}}? _currentItem;
    private readonly Request _request;
    private readonly Operation _operation;
    private readonly WebApiConfiguration _configuration;
    
    /// <summary>
    /// Construct response for content {{_contentType}}
    /// </summary>
    /// <param name="request">Request</param>{{(_isContentTypeRange ? 
$"""

   /// <param name="contentType">Content type must match range {_contentType.MediaType}</param>
""" : "")}}
    public {{ClassName}}(Request request{{(_isContentTypeRange ? ", string contentType" : "")}})
    {{{(_isContentTypeRange ? 
"""
        
        EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), ContentMediaType);
""" : "")}}
        _request = request;
        _content = new(request.HttpContext.Response.BodyWriter);
        _operation = request.HttpContext.RequestServices.GetRequiredService<Operation>();
        _configuration = request.HttpContext.RequestServices.GetRequiredService<WebApiConfiguration>();
        {{contentTypeFieldName}} = {{(_isContentTypeRange ? "contentType" : $"\"{_contentType.MediaType}\"")}};
    }

    /// <summary>
    /// Write an item to the sequence
    /// </summary>
    /// <param name="item">Item to write</param>
    internal void WriteItem({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} item)
    {
        _currentItem = item;
        _operation.Validate(this, _configuration);
        
        WriteHeaders(_request.HttpContext.Response);
        _content.WriteItem(item);
        _currentItem = null;
    }
    
    internal static ContentMediaType<{{responseClassName}}> ContentMediaType { get; } = new(MediaTypeHeaderValue.Parse("{{_contentType}}"));
    /// <inheritdoc/>
    internal override void WriteTo(HttpResponse httpResponse)
    {
        WriteHeaders(httpResponse);
        _content.Dispose();
    }
    
    private void WriteHeaders(HttpResponse httpResponse)
    {
        if (!httpResponse.HasStarted)
        {
            base.WriteTo(httpResponse);
        }
    }
    
    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var context = ValidateHeaders(validationLevel);
        return ValidateCurrentItem(context, validationLevel);
    }
    
    private ValidationContext ValidateHeaders(ValidationLevel validationLevel) =>
        _request.HttpContext.Response.HasStarted
            ? CreateValidationContext()
            : base.Validate(validationLevel);

    private ValidationContext ValidateCurrentItem(
        ValidationContext validationContext, 
        ValidationLevel validationLevel) =>
        _currentItem is null
            ? validationContext
            : _content.Validate(_currentItem.Value, ContentSchemaLocation, validationContext,
                validationLevel);
}                              
""" :


$$"""
/// <summary>
/// Response for content {{_contentType}}
/// </summary>
internal sealed class {{ClassName}} : {{responseClassName}}
{
    private {{_typeDeclaration.FullyQualifiedDotnetTypeName()}} _content;
    
    /// <summary>
    /// Construct response for content {{_contentType}}
    /// </summary>
    /// <param name="{{_contentVariableName}}">Content</param>{{(_isContentTypeRange ? 
$"""

    /// <param name="contentType">Content type must match range {_contentType.MediaType}</param>
""" : "")}}
    public {{ClassName}}({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}}{{(_isContentTypeRange ? ", string contentType" : "")}})
    {{{(_isContentTypeRange ? 
"""
        
        EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), ContentMediaType);
""" : "")}}
        _content = {{_contentVariableName}};
        {{contentTypeFieldName}} = {{(_isContentTypeRange ? "contentType" : $"\"{_contentType.MediaType}\"")}};
    }
    
    internal static ContentMediaType<{{responseClassName}}> ContentMediaType { get; } = new(MediaTypeHeaderValue.Parse("{{_contentType}}"));
    /// <inheritdoc/>
    internal override void WriteTo(HttpResponse httpResponse)
    {
        base.WriteTo(httpResponse);
        httpResponse.WriteResponseBody(_content);
    }
    
    private const string ContentSchemaLocation = "{{SchemaLocation}}";
    /// <inheritdoc/>
    internal override ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = base.Validate(validationLevel);
        return _content.Validate(ContentSchemaLocation, true, validationContext, validationLevel);
    }
}
""";
}
