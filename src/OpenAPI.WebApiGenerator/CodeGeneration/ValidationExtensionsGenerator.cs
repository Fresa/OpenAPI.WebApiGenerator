namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ValidationExtensionsGenerator(string @namespace)
{
    private const string ClassName = "ValidationExtensions";
    internal SourceCode GenerateClass() => new($"{ClassName}.g.cs", 
$$"""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;

namespace {{@namespace}};

/// <summary>
/// Extension methods for validation
/// </summary>
internal static class {{ClassName}}
{
    /// <summary>
    /// Add schema location to validation results
    /// </summary>
    /// <param name="validationResults">Validation results to add schema location to</param>
    /// <param name="uri">The schema location uri</param>
    /// <returns>The validation results</returns>
    internal static ImmutableList<ValidationResult> WithLocation(
        this ImmutableList<ValidationResult> validationResults, Uri? uri)
    {
        if (uri == null)
        {
            return validationResults;
        }
        var pathUri = uri.GetLeftPart(UriPartial.Path);
        return validationResults
            .Select(result =>
                new ValidationResult(result.Valid, result.Message, GetLocation(result.Location, pathUri)))
            .ToImmutableList();
    }

    private static (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)? GetLocation(
        (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)? location, string uri)
    {
        if (location == null)
            return location;
        var schemaLocation = new JsonReference(uri.AsSpan(), location.Value.SchemaLocation.Fragment);
        return (location.Value.ValidationLocation, schemaLocation, location.Value.DocumentLocation);
    }
    
    /// <summary>
    /// Validate a json object
    /// </summary>
    /// <param name="value">json object to validate</param>
    /// <param name="schemaLocation">The location of the schema describing the json object</param>
    /// <param name="isRequired">Is the object required?</param>
    /// <param name="validationContext">Current validation context</param>
    /// <param name="validationLevel">The validation level</param>
    /// <returns>The validation result</returns>
    internal static ValidationContext Validate<T>(this T value,
        string schemaLocation, 
        bool isRequired,
        ValidationContext validationContext,
        ValidationLevel validationLevel) 
        where T : struct, IJsonValue<T>
    {
        if (!isRequired && value.IsUndefined())
        {
            return validationContext;
        }
      
        var context = validationContext.PushSchemaLocation(schemaLocation);
        context = value.Validate(context, validationLevel);
        return context.PopLocation();
    }
}
#nullable restore
""");
}