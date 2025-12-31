namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class JsonValidationExceptionGenerator(string @namespace)
{
    private const string ClassName = "JsonValidationException";
    internal string CreateThrowJsonValidationExceptionInvocation(
        string message, 
        string validationResultVariableName)
    {
        return
            $"""throw new {@namespace}.{ClassName}("{message}", {validationResultVariableName})""";
    }
    internal SourceCode GenerateJsonValidationExceptionClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using Corvus.Json;
        using System;
        using System.Collections.Immutable;
        using System.Text;
        
        namespace {{@namespace}};
              
        internal sealed class {{ClassName}} : Exception 
        {
            internal {{ClassName}}(string message, ImmutableList<ValidationResult> validationResult) : base(
                GetValidationMessage(message, validationResult))
            {
                ValidationResult = validationResult;
            }

            internal ImmutableList<ValidationResult> ValidationResult { get; }

            private static string GetValidationMessage(string message, ImmutableList<ValidationResult> validationResult)
            {
                return validationResult.IsEmpty
                    ? message
                    : validationResult.Aggregate(
                        new StringBuilder($"{message}:").AppendLine(), 
                        (builder, result) => 
                            builder.AppendLine($"- {result}")).ToString();
            }
        }
        #nullable restore
        """);
}