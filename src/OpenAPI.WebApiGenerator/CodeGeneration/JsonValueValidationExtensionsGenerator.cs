namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class JsonValueValidationExtensionsGenerator(string @namespace)
{
    private const string ClassName = "JsonValueValidationExtensions";
    
    internal string CreateValidateInvocation(
        string jsonValueVariableName, 
        string isRequiredVariableName)
    {
        return
            $"{jsonValueVariableName}.Validate({isRequiredVariableName})";
    }
    
    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using Corvus.Json;
        using Microsoft.AspNetCore.Http;
        using System.Text;
        
        namespace {{@namespace}};

        internal static class {{ClassName}}
        {
            internal static ValidationContext Validate<T>(this T value, bool isRequired) 
                where T : struct, IJsonValue
            {
                if (!isRequired && value.IsUndefined())
                {
                    return value;
                }
                
                var validationContext = ValidationContext.ValidContext;
                validationContext = value.Validate(validationContext, ValidationLevel.Detailed);
                return validationContext;
            }
        }
        #nullable restore
        """);
}