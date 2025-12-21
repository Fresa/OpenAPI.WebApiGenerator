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
            internal static T Validate<T>(this T value, bool isRequired) 
                where T : struct, IJsonValue
            {
                if (!isRequired && value.IsUndefined())
                {
                    return value;
                }
                
                var validationContext = ValidationContext.ValidContext;
                validationContext = value.Validate(validationContext, ValidationLevel.Detailed);
                if (validationContext.IsValid)
                {
                    return value;
                }

                var validationResults = validationContext.Results.IsEmpty
                    ? "None"
                    : validationContext.Results.Aggregate(
                        new StringBuilder($"Object of type {typeof(T)} is not valid"), 
                        (builder, result) => 
                            builder.AppendLine(result.ToString())).ToString();

                throw new BadHttpRequestException(validationResults);
            }
        }
        #nullable restore
        """);
}