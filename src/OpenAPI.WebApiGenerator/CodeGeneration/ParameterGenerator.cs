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
    
    private readonly string _propertyName = parameter.GetName().ToPascalCase();
    
    internal string GenerateRequestProperty()
    {
        return $$"""
                internal {{(parameter.Required ? "required " : "")}}{{FullyQualifiedTypeName}} {{_propertyName}} { get; init; }
                """;
    }

    internal string GenerateRequestBindingDirective(string requestVariableName)
    {
        using var textWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings()
        {
            InlineLocalReferences = true
        });
        parameter.SerializeAsV2(jsonWriter);
        textWriter.Flush();

        return $" {_propertyName} = {httpRequestExtensionsGenerator.CreateBindParameterInvocation(
            requestVariableName,
            FullyQualifiedTypeName.TrimEnd('?'),
            textWriter.GetStringBuilder().ToString())}{(parameter.Required ? "" : ".AsOptional()")},";
    }
}