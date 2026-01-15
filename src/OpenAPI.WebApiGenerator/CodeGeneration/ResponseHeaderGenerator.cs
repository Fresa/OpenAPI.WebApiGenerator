using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseHeaderGenerator(
    string name, 
    IOpenApiHeader header, 
    TypeDeclaration typeDeclaration, 
    HttpResponseExtensionsGenerator httpResponseExtensionsGenerator)
{
    private readonly string _propertyName = name.ToPascalCase();
    private readonly string _requiredDirective = header.Required ? "required" : string.Empty;
    private string DefaultValueAssignment => header.Required ? "" : $" = {FullyQualifiedTypeName}.Undefined;";
    private string FullyQualifiedTypeName =>
        $"{_fullyQualifiedTypeDeclarationIdentifier}";
    private readonly string _fullyQualifiedTypeDeclarationIdentifier = typeDeclaration.FullyQualifiedDotnetTypeName();

    internal bool IsRequired { get; } = header.Required;
    
    internal string GenerateProperty() =>
        $$"""
          internal {{_requiredDirective}} {{FullyQualifiedTypeName}} {{_propertyName}} { get; init; }{{DefaultValueAssignment}}
          """;
    
    internal string GenerateWriteDirective(string responseVariableName)
    {
        var headerSpecificationAsJson = httpResponseExtensionsGenerator.GetResponseHeaderSpecificationAsJson(header, name);
        return
            $""""
             {responseVariableName}.WriteResponseHeader(
                 OpenApiVersion,
                 """
                 {headerSpecificationAsJson.Indent(4).TrimStart()}
                 """,
                 "{name}",
                 Headers.{_propertyName},
                 {header.Required.ToString().ToLowerInvariant()});
             """";
    }
}
