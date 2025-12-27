using System.Linq;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseBodyContentGenerator(string contentType, string statusCodePattern, TypeDeclaration typeDeclaration)
{
    private readonly string _contentVariableName = contentType.ToCamelCase();
    public string ContentPropertyName { get; } = contentType.ToPascalCase();
    
    private readonly string _statusCodeArgumentName = "statusCode";
    private readonly bool _hasExplicitStatusCode = int.TryParse(statusCodePattern, out _);
    
    public string GenerateConstructor(string className, string contentTypeFieldName, string statusCodeFieldName)
    {
        return
            $$"""
                public {{className}}({{GenerateStatusCodeArgument()}}{{typeDeclaration.FullyQualifiedDotnetTypeName()}} {{_contentVariableName}})
                {
                    {{ContentPropertyName}} = {{_contentVariableName}};
                    {{contentTypeFieldName}} = "{{contentType}}";
                    {{statusCodeFieldName}} = {{GenerateStatusCodeAssignment()}};
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

    private string GenerateStatusCodeArgument() => 
        _hasExplicitStatusCode ? string.Empty : $"int {_statusCodeArgumentName}, ";

    private string GenerateStatusCodeAssignment() =>
        statusCodePattern switch
        {
            "default" => _statusCodeArgumentName,
            _ when _hasExplicitStatusCode => statusCodePattern,
            _ => $"Validate{statusCodePattern.First()}xxStatusCode({_statusCodeArgumentName})"
        };
}
