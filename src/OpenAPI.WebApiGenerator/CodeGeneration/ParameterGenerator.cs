using System.IO;
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
    private string FullyQualifiedTypeName =>
        $"{FullyQualifiedTypeDeclarationIdentifier}{(parameter.Required ? "" : "?")}";

    private string FullyQualifiedTypeDeclarationIdentifier => typeDeclaration.FullyQualifiedDotnetTypeName();
    
    internal string PropertyName { get; } = parameter.GetName().ToPascalCase();
    internal bool IsParameterRequired { get; } = parameter.Required;
    
    internal string GenerateRequestProperty()
    {
        return $$"""
                internal {{(IsParameterRequired ? "required " : "")}}{{FullyQualifiedTypeName}} {{PropertyName}} { get; init; }
                """;
    }

    internal string AsRequired(string variableName) => $"{variableName}{(IsParameterRequired ? "" : $" ?? {FullyQualifiedTypeDeclarationIdentifier}.Undefined")}";
    
    internal string GenerateRequestBindingDirective(string requestVariableName)
    {
        using var textWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings()
        {
            InlineLocalReferences = true
        });
        parameter.SerializeAsV2(jsonWriter);
        textWriter.Flush();

        return $" {PropertyName} = {httpRequestExtensionsGenerator.CreateBindParameterInvocation(
            requestVariableName,
            FullyQualifiedTypeDeclarationIdentifier,
            textWriter.GetStringBuilder().ToString(),
            IsParameterRequired)}{(IsParameterRequired ? "" : ".AsOptional()")},";
    }
}