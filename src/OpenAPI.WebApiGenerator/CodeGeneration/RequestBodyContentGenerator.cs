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

    internal string ContentType => contentType;
    
    internal string GenerateRequestBindingDirective(bool isRequired)
    {
        return $"""
                 {PropertyName} = 
                    ({httpRequestExtensionsGenerator.CreateBindBodyInvocation(
                        "request", 
                        FullyQualifiedTypeDeclarationIdentifier,
                        isRequired)})
                        .AsOptional()
                """;
    }
                 
    public string GenerateRequestProperty()
    {
        return $$"""
                 internal {{FullyQualifiedTypeName}} {{PropertyName}} { get; private set; }
                 """;
    }
}