using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OpenAPI.WebApiGenerator.CodeGeneration;
using OpenAPI.WebApiGenerator.Tests.Utils;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    // Base framework references so generated types resolve; built once and shared.
    private static readonly MetadataReference[] RuntimeReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => !string.IsNullOrEmpty(path))
        .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
        .ToArray();

    private static void HasOnlyMissingHandler(ImmutableArray<Diagnostic> diagnostics)
    {
        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Id.Should().Be("AF1001", diagnostic.GetFormattedMessage());
        });
    }

    private Compilation SetupGenerator(string openApiSpec, out ImmutableArray<Diagnostic> diagnostics)
    {
        var generator = new ApiGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.AddAdditionalTexts(
            [
                new InMemoryAdditionalText("openapi.json",
                    openApiSpec)
            ]
        );

        const string assemblyName = nameof(ApiGeneratorTests);
        var compilation = CSharpCompilation.Create(assemblyName,
            references: RuntimeReferences,
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out diagnostics,
            Cancellation);

        foreach (var tree in newCompilation.SyntaxTrees)
        {
            tree.GetDiagnostics().Should().NotContain(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error ||
                diagnostic.Severity == DiagnosticSeverity.Warning);
        }

        var errorsCausedByMissingReferences = new[]
        {
            "CS0518", // predefined type is not defined or imported
            "CS0656", // missing compiler-required member
            "CS0012", // type is defined in an assembly that is not referenced
            "CS1069", // type could not be found in a namespace, per the using
            "CS0234", // type or namespace does not exist in the namespace
            "CS0246", // type or namespace could not be found
            "CS0400", // The type or namespace name could not be found in the global namespace (are you missing an assembly reference?)
            "CS8179", // Predefined type System.ValueTuple is not defined or imported
            "CS0103", // name does not exist in the current context
            "CS1061", // no definition for member (type unresolved)
            "CS0538", // explicit interface declaration is not an interface
            "CS1729", // type has no constructor with that many arguments
            "CS0314", // type cannot be a type parameter (constraint unresolved)
            "CS0305", // wrong number of type arguments (generic unresolved)
            "CS0704", // non-virtual member lookup on unresolved type
            "CS9174", // collection-expression init on unresolved type
            "CS8137", // cannot define a member on an unresolved type
            "CS1110", // cannot define an extension on an unresolved type
            "CS0229", // ambiguity between members (unresolved base)
            "CS0121", // ambiguous call (unresolved overloads)
            "CS1955", // non-invocable member used like a method
            "CS0161", // not all code paths return a value (unresolved return type)
            "CS0315", // no boxing conversion for type parameter (constraint unresolved)
            "CS8919"
        };

        var compilationDiagnostics = newCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => !errorsCausedByMissingReferences.Contains(diagnostic.Id))
            .ToArray();

        compilationDiagnostics.Should().BeEmpty(because:
            "the generated code should be valid C#, but found:\n" +
            string.Join("\n", compilationDiagnostics.Select(diagnostic => diagnostic.ToString())) +
            "\n\n" +
            string.Join("\n\n", compilationDiagnostics
                .Select(diagnostic => diagnostic.Location.SourceTree)
                .Distinct()
                .Select(tree =>
                    $"""
                     // === {tree?.FilePath} ===
                     {tree?.GetText()}
                     """)));

        return newCompilation;
    }
}