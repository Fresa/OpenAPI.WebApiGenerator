using System;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator
{
    private readonly string _contentVariableName;
    internal string ClassName { get; }
    private readonly MediaTypeHeaderValue _contentType;
    private readonly TypeDeclaration _typeDeclaration;
    private readonly bool _isContentTypeRange;

    public ResponseBodyContentGenerator(string contentType, TypeDeclaration typeDeclaration)
    {
        _contentType = MediaTypeHeaderValue.Parse(contentType);
        _typeDeclaration = typeDeclaration;
        ClassName = contentType.ToPascalCase();

        _isContentTypeRange = false;
        switch (_contentType.MediaType)
        {
            case "*/*":
                _contentVariableName = "any";
                _isContentTypeRange = true;
                break;
            case not null when _contentType.MediaType.EndsWith("*"):
                _contentVariableName = $"any{_contentType.MediaType.TrimEnd('*').TrimEnd('/').ToPascalCase()}";
                _isContentTypeRange = true;
                break;
            case null:
                throw new InvalidOperationException("Content type is null");
            default:
                _contentVariableName = _contentType.MediaType.ToCamelCase();
                break;
        }

        ClassName = _contentVariableName.ToPascalCase();
    }

    private string SchemaLocation => _typeDeclaration.RelativeSchemaLocation;
    public string GenerateResponseClass(string responseClassName, string contentTypeFieldName) =>
$$"""
/// <summary>
/// Response for content {{_contentType}}
/// </summary>
internal sealed class {{ClassName}} : {{responseClassName}}
{
    /// <summary>
    /// Construct response for content {{_contentType}}
    /// </summary>
    /// <param name="{{_contentVariableName}}">Content</param>{{(_isContentTypeRange ? $"""

             /// <param name="contentType">Content type must match range {_contentType.MediaType}</param>
         """ : "")}}
    public {{ClassName}}({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}}{{(_isContentTypeRange ? ", string contentType" : "")}})
    {{{(_isContentTypeRange ? 
"""
        
        EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), ContentMediaType);
""" : "")}}
        Content = {{_contentVariableName}};
        {{contentTypeFieldName}} = {{(_isContentTypeRange ? "contentType" : $"\"{_contentType.MediaType}\"")}};
    }
    
    /// <inheritdoc/>
    public static ContentMediaType<{{responseClassName}}> ContentMediaType { get; } = new(MediaTypeHeaderValue.Parse("{{_contentType}}"));
    protected override IJsonValue Content { get; }
    protected override string ContentSchemaLocation { get; } = "{{SchemaLocation}}";
}
""";
}
