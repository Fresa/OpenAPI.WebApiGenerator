namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class JsonValidationExceptionGenerator(string @namespace)
{
    internal const string ClassName = "JsonValidationException";
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
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        using System.Text;
        
        namespace {{@namespace}};
              
        /// <summary>
        /// Exception thrown when validation of json objects fail
        /// </summary>
        internal sealed class {{ClassName}} : Exception 
        {
            /// <summary>
            /// Create json validation exception
            /// </summary>
            internal {{ClassName}}(string message, ImmutableList<ValidationResult> validationResult) : base(
                    GetValidationMessage(message, validationResult))
            {
                ValidationResult = validationResult;
            }

            /// <summary>
            /// The validation result
            /// </summary>
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