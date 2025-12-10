using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator(string contentType, TypeDeclaration typeDeclaration)
{
    private readonly string _contentVariableName = contentType.ToCamelCase();
    public string ContentPropertyName { get; } = contentType.ToPascalCase();
    
    public string GenerateConstructor(string className)
    {
        return
            $$"""
                public {{className}}({{typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}})
                {
                    {{ContentPropertyName}} = {{_contentVariableName}};
                }          
              """; 
    }
    
    public string GenerateContentProperty()
    {
        return
            $$"""
                internal {{typeDeclaration.FullyQualifiedDotnetTypeName()}}? {{ContentPropertyName}} { get; }          
              """; 
    }
}
