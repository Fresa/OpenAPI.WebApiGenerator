using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OpenAPI.WebApiGenerator.CodeGeneration;
using OpenAPI.WebApiGenerator.Tests.Utils;
using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;
    
    [Theory]
    [InlineData("openapi-v2.json")]
    [InlineData("openapi-v3.json")]
    [InlineData("openapi-v3.1.json")]
    [InlineData("openapi-v3.2.json")]
    [InlineData("openapi-v3.2.yaml")]
    public void GivenAnOpenAPISpec_WhenGeneratingAPI_ExpectedClassesShouldHaveBeenGenerated(string specFile)
    {
        var generator = new ApiGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.AddAdditionalTexts(
            [
                new TestAdditionalFile($"OpenApiSpecs/{specFile}")
            ]
        );

        var compilation = CSharpCompilation.Create(nameof(ApiGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out var diagnostics, Cancellation);

        // Operation handler stubs should be generated with a warning
        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Id.Should().Be("AF1001", diagnostic.GetFormattedMessage());
        });

        var generatedFiles = newCompilation.SyntaxTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToArray();

        generatedFiles.Should().HaveCountGreaterThan(0);
        generatedFiles.Should().ContainMatch("*.Request.g.cs");
        generatedFiles.Should().ContainMatch("*.Response.g.cs");
        generatedFiles.Should().ContainMatch("*.Operation.g.cs");
    }

    
    [Theory]
    [MemberData(nameof(OpenApiSpecsWithOperations))]
    public void GivenAImplementedOperation_WhenGeneratingAPI_NoOperationHandlerStubsShouldBeGenerated(
        string _, string openApiSpec)
    {
        var generator = new ApiGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.AddAdditionalTexts(
            [
                new InMemoryAdditionalText("openapi.json", openApiSpec)
            ]
        );

        const string assemblyName = nameof(ApiGeneratorTests);
        var compilation = CSharpCompilation.Create(assemblyName,
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));

        var implementedOperationSourceCode = CSharpSyntaxTree.ParseText(SourceText.From(
            $$"""
            namespace {{assemblyName}}.Paths.Foo.Put
            {
                internal partial class Operation
                {
                    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            """
        ), cancellationToken: Cancellation);
        implementedOperationSourceCode.GetDiagnostics(Cancellation).Should().BeEmpty();
        compilation = compilation.AddSyntaxTrees(implementedOperationSourceCode);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out var diagnostics,
            Cancellation);

        diagnostics.Should().BeEmpty();

        newCompilation.SyntaxTrees.Should().HaveCountGreaterThan(0);
        var operationType = newCompilation.GetSymbolsWithName("Operation", cancellationToken: Cancellation)
            .OfType<INamedTypeSymbol>()
            .Where(symbol => symbol.ContainingNamespace.ToDisplayString() == $"{assemblyName}.Paths.Foo.Put")
            .Should().HaveCount(1).And.Subject.First();
        var handleAsyncSymbols = operationType.GetMembers("HandleAsync")
            .OfType<IMethodSymbol>()
            .Should().HaveCountGreaterThanOrEqualTo(1, "there should be at least one implementation of HandleAsync")
            .And.Subject;

        var handleAsyncSymbol = handleAsyncSymbols.Should()
            .ContainSingle(symbol => symbol.Parameters.Length == 2, "there should be a handler with two parameters; request and cancellation token")
            .Subject;
        handleAsyncSymbol.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Should()
            .Be("Request");
        handleAsyncSymbol.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Should()
            .Be("CancellationToken");
        handleAsyncSymbol.PartialImplementationPart.Should().NotBeNull();

        var generatedFiles = newCompilation.SyntaxTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToArray();

        generatedFiles.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(NoResponseContentSpecs))]
    public void NoResponseContent_Generating_DefaultResponseConstructor(string _, string openApiSpec)
    {
        var compilation = SetupGenerator(openApiSpec,
            out var diagnostics);
        HasOnlyMissingHandler(diagnostics);
        compilation.SyntaxTrees.Should().HaveCountGreaterThan(0);
        var responseType = compilation.GetSymbolsWithName("Accepted202", cancellationToken: Cancellation)
            .OfType<INamedTypeSymbol>()
            .Where(symbol => symbol.ContainingNamespace.ToDisplayString() == $"{compilation.AssemblyName}.Paths.Foo.Delete")
            .Should().HaveCount(1).And.Subject.First();
        responseType.Constructors.Should().HaveCount(1)
            .And.Subject.First()
            .Parameters.Should().HaveCount(0);
    }

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
            options: new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out diagnostics,
            Cancellation);
        return newCompilation;
    }
}
