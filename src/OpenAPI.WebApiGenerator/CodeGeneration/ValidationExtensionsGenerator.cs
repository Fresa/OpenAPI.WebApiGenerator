namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ValidationExtensionsGenerator(string @namespace)
{
    private const string ClassName = "ValidationResultsExtensions";
    internal SourceCode GenerateClass() => new($"{ClassName}.g.cs", 
$$"""
#nullable enable
using Corvus.Json;
using System.Collections.Immutable;

namespace {{@namespace}};

internal static class {{ClassName}}
{
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
}
#nullable restore
""");
}