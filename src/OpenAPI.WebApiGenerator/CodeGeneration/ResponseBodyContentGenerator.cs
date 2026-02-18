using System;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator
{
    private readonly string _contentVariableName;
    public string ContentPropertyName { get; }
    private readonly MediaTypeHeaderValue _contentType;
    private readonly TypeDeclaration _typeDeclaration;
    private readonly bool _isContentTypeRange;

    public ResponseBodyContentGenerator(string contentType, TypeDeclaration typeDeclaration)
    {
        _contentType = MediaTypeHeaderValue.Parse(contentType);
        _typeDeclaration = typeDeclaration;
        ContentPropertyName = contentType.ToPascalCase();

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

        ContentPropertyName = _contentVariableName.ToPascalCase();
    }

    internal string SchemaLocation => _typeDeclaration.RelativeSchemaLocation;
    public string GenerateConstructor(string className, string contentTypeFieldName) =>
$$"""
/// <summary>
/// Construct content for {{_contentType}}
/// </summary>
/// <param name="{{_contentVariableName}}">Content</param>{{(_isContentTypeRange ? $"""

/// <param name="contentType">Content type must match range {_contentType.MediaType}</param>
""" : "")}}
public {{className}}({{_typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}}{{(_isContentTypeRange ? ", string contentType" : "")}})
{{{(_isContentTypeRange ? 
$$"""
    
    EnsureExpectedContentType(MediaTypeHeaderValue.Parse(contentType), MediaTypeHeaderValue.Parse("{{_contentType}}"));
""" : "")}}
    {{ContentPropertyName}} = {{_contentVariableName}};
    {{contentTypeFieldName}} = {{(_isContentTypeRange ? "contentType" : $"\"{_contentType.MediaType}\"")}};
}
""";

    public string GenerateContentProperty()
    {
        return
$$"""
/// <summary>
/// Content for {{_contentType}}
/// </summary>
internal {{_typeDeclaration.FullyQualifiedDotnetTypeName()}}? {{ContentPropertyName}} { get; }          
"""; 
    }
}
