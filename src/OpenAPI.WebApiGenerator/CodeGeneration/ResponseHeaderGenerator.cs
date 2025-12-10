using System.IO;
using System.Linq;
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
    private readonly string _nullableDirective = header.Required ? string.Empty : "?";
    private string FullyQualifiedTypeName =>
        $"{_fullyQualifiedTypeDeclarationIdentifier}{_nullableDirective}";
    private readonly string _fullyQualifiedTypeDeclarationIdentifier = typeDeclaration.FullyQualifiedDotnetTypeName();

    internal bool IsRequired { get; } = header.Required;
    
    internal string GenerateProperty() =>
        $$"""
          internal {{_requiredDirective}} {{FullyQualifiedTypeName}} {{_propertyName}} { get; init; }          
          """;
    
    internal string GenerateWriteDirective(string responseVariableName)
    {
        using var textWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings
        {
            InlineLocalReferences = true
        });
        header.SerializeAsV2(jsonWriter);
        textWriter.Flush();

        // Response header specification is a subset of the parameter specification, so we add the missing properties to be able to use the parameter value parser 
        var headerSpecificationAsJson = 
            $$"""
              {
                "name": "{{name}}",
                "in": "header",
                {{textWriter.GetStringBuilder().ToString().TrimStart('{').TrimStart()}} 
              """;
        
        return $"{httpResponseExtensionsGenerator.CreateWriteHeaderInvocation(
            responseVariableName,
            FullyQualifiedTypeName.TrimEnd('?'),
            headerSpecificationAsJson,
            name,
            _propertyName
            )}{(IsRequired ? "" : ".AsOptional()")},";
    }
}
