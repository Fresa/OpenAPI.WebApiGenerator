using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    [Theory]
    [MemberData(nameof(AnonymousSecurityRequirementSpecs))]
    public void GivenAnonymousSecurityRequirement_WhenGenerating_SecurityRequirementsFilterRepresentsEachRequirementObject(
        string _, string openApiSpec)
    {
        var sourceCode = GetOperationSourceCode(openApiSpec);

        sourceCode.Should().Contain("class SecurityRequirementsFilter");
        sourceCode.Should().Contain("new SecurityRequirement", Exactly.Twice());
        sourceCode.Should().Contain("[\"secret_key\"] = []", Exactly.Once());
    }

    private string GetOperationSourceCode(string openApiSpec)
    {
        var compilation = SetupGenerator(openApiSpec, out var diagnostics);
        HasOnlyMissingHandler(diagnostics);

        var operationSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(t => Path.GetFileName(t.FilePath).EndsWith("Operation.g.cs"));
        operationSyntaxTree.Should().NotBeNull();

        return operationSyntaxTree.ToString();
    }
}