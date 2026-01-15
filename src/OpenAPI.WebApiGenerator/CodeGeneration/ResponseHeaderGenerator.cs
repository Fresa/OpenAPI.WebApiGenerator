using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;
using OpenAPI.WebApiGenerator.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseHeaderGenerator(
    string name, 
    IOpenApiHeader header, 
    TypeDeclaration typeDeclaration, 
    OpenApiSpecVersion openApiSpecVersion)
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
        // Response header specification is a subset of the parameter specification, so we add the missing properties to be able to use the parameter value parser 
        var headerSpecificationAsJson = 
            $$"""
              {
                "name": "{{name}}",
                "in": "header",
                {{header.Serialize(openApiSpecVersion).ToString().TrimStart('{').TrimStart()}} 
              """;

        return
            $""""
             {responseVariableName}.WriteResponseHeader(
                 "{openApiSpecVersion.GetParameterVersion()}",
                 """
                 {headerSpecificationAsJson.Indent(4).TrimStart()}
                 """,
                 "{name}",
                 Headers.{_propertyName},
                 {header.Required.ToString().ToLowerInvariant()});
             """";
    }
}
