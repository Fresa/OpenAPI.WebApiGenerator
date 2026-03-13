using System.Collections.Generic;
using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class RequestBodyContentGenerator(
    KeyValuePair<string, IOpenApiMediaType> contentMediaType, 
    TypeDeclaration typeDeclaration,
    HttpRequestExtensionsGenerator httpRequestExtensionsGenerator,
    SequentialJsonEnumeratorGenerator sequentialJsonEnumeratorGenerator)
{
    private string FullyQualifiedTypeDeclarationIdentifier => typeDeclaration.FullyQualifiedDotnetTypeName();
    private readonly bool _isSequentialMediaType = contentMediaType.Value.ItemSchema != null;
    
    internal string PropertyName { get; } = contentMediaType.Key.ToPascalCase();
    internal bool IsPropertyStruct => !_isSequentialMediaType; 
    
    internal MediaTypeWithQualityHeaderValue ContentType { get; } = MediaTypeWithQualityHeaderValue.Parse(contentMediaType.Key);

    internal string SchemaLocation => typeDeclaration.RelativeSchemaLocation;
    internal string GenerateRequestBindingDirective() =>
$"""
{PropertyName} = {(_isSequentialMediaType ?
    $"{sequentialJsonEnumeratorGenerator.GenerateConstructorInstance(
        ContentType,
        typeDeclaration, 
        "request.Body",
        "cancellationToken")}" : 
    $"({httpRequestExtensionsGenerator.CreateBindBodyInvocation(
        "request", 
        FullyQualifiedTypeDeclarationIdentifier).Indent(8).Trim()})")}
""";
    

    public string GenerateRequestProperty()
    {
        var fullyQualifiedTypeName = _isSequentialMediaType
            ? sequentialJsonEnumeratorGenerator.GetFullyQualifiedTypeName(ContentType, typeDeclaration)
            : FullyQualifiedTypeDeclarationIdentifier;
        return 
$$"""
/// <summary>
/// Request content for {{contentMediaType.Key}}
/// </summary>
internal {{fullyQualifiedTypeName}}? {{PropertyName}} { get; private set; }
""";
    }
}