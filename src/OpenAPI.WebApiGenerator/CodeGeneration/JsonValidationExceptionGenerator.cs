namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class JsonValidationExceptionGenerator(string @namespace)
{
    private const string ClassName = "JsonValidationException";
    internal string CreateThrowJsonValidationExceptionInvocation(
        string messageVariableName, 
        string validationResultVariableName)
    {
        return
            $"throw new {ClassName}({messageVariableName}, {validationResultVariableName})";
    }
    internal SourceCode GenerateJsonValidationExceptionClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using Corvus.Json;
        using System;
        
        namespace {{@namespace}};
              
        internal sealed class {{ClassName}} : Exception 
        {
            internal JsonValidationException(string message, ImmutableList<ValidationResult> validationResult) : base(
                GetValidationMessage(validationResult))
            {
                //var validationMessage = $"Object of type {typeof(T)} is not valid";
            }

            internal ImmutableList<ValidationResult> ValidationResult => validationResult;

            private static string GetValidationMessage(ImmutableList<ValidationResult> validationResult)
            {
                return validationContext.Results.IsEmpty
                    ? validationMessage
                    : validationContext.Results.Aggregate(
                        new StringBuilder($"{validationMessage}:").AppendLine(), 
                        (builder, result) => 
                            builder.AppendLine($"- {result}")).ToString()
            }
        }
        #nullable restore
        """);
}