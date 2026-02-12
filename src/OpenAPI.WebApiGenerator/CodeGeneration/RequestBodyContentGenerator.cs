using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class RequestBodyContentGenerator(
    string contentType, 
    TypeDeclaration typeDeclaration,
    HttpRequestExtensionsGenerator httpRequestExtensionsGenerator)
{
    private string FullyQualifiedTypeName =>
        $"{FullyQualifiedTypeDeclarationIdentifier}?";

    private string FullyQualifiedTypeDeclarationIdentifier => typeDeclaration.FullyQualifiedDotnetTypeName();

    internal string PropertyName { get; } = contentType.ToPascalCase();

    internal MediaTypeHeaderValue ContentType { get; } = MediaTypeHeaderValue.Parse(contentType);

    internal string SchemaLocation => typeDeclaration.RelativeSchemaLocation;
    internal string GenerateRequestBindingDirective() =>
$"""
{PropertyName} = 
    ({httpRequestExtensionsGenerator.CreateBindBodyInvocation(
            "request", 
            FullyQualifiedTypeDeclarationIdentifier)
        .Indent(8).Trim()})
    .AsOptional()
""";

    public string GenerateRequestProperty() =>
        $$"""
          /// <summary>
          /// Request content for {{contentType}}
          /// </summary>
          internal {{FullyQualifiedTypeName}} {{PropertyName}} { get; private set; }
          """;
}