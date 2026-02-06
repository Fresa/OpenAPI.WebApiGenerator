using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;
using OpenAPI.WebApiGenerator.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ParameterGenerator(
    TypeDeclaration typeDeclaration, 
    IOpenApiParameter parameter,
    HttpRequestExtensionsGenerator httpRequestExtensionsGenerator)
{
    internal string FullyQualifiedTypeName =>
        $"{FullyQualifiedTypeDeclarationIdentifier}{(parameter.Required ? "" : "?")}";

    private string FullyQualifiedTypeDeclarationIdentifier => typeDeclaration.FullyQualifiedDotnetTypeName();
    
    internal string PropertyName { get; } = parameter.GetName().ToPascalCase();
    internal bool IsParameterRequired { get; } = parameter.Required;
    internal string Location { get; } = parameter.GetLocation();
    internal string SchemaLocation { get; } = typeDeclaration.RelativeSchemaLocation;
    
    internal string GenerateRequestProperty() =>
        $$"""
          internal {{(IsParameterRequired ? "required " : "")}}{{FullyQualifiedTypeName}} {{PropertyName}} { get; init; }
          """;

    internal string AsRequired(string variableName) => $"{variableName}{(IsParameterRequired ? "" : $" ?? {FullyQualifiedTypeDeclarationIdentifier}.Undefined")}";
    
    internal string GenerateRequestBindingDirective(string requestVariableName) =>
        $"{PropertyName} = {httpRequestExtensionsGenerator.CreateBindParameterInvocation(
                requestVariableName,
                FullyQualifiedTypeDeclarationIdentifier,
                parameter)
            .Indent(4).TrimStart()}{(IsParameterRequired ? "" : ".AsOptional()")},";

    internal bool IsSecuritySchemeParameter(IOpenApiSecurityScheme scheme) =>
        scheme.In == parameter.In &&
        scheme.Name == parameter.Name;
}