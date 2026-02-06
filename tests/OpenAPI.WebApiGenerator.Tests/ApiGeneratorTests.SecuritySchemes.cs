using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    [Theory]
    [MemberData(nameof(ApiKeySecuritySchemeWithMatchingParameterSpecs))]
    public void GivenApiKeySecuritySchemeWithMatchingParameter_WhenGenerating_SecuritySchemesClassHasGetParameterMethod(
        string _, string openApiSpec)
    {
        var sourceCode = GetSecuritySchemesSourceCode(openApiSpec);

        sourceCode.Should().Contain("internal const string SecretKeyKey = \"secret_key\";");
        sourceCode.Should().Contain("internal static class SecretKey");
        sourceCode.Should().Contain("GetParameter(HttpContext context)");
        sourceCode.Should().NotContain("TryGetParameter(HttpContext context, out");
    }

    [Theory]
    [MemberData(nameof(ApiKeySecuritySchemeWithMultipleParameterTypesSpecs))]
    public void GivenApiKeySecuritySchemeWithMultipleParameterTypes_WhenGenerating_SecuritySchemesClassHasTryGetParameterMethods(
        string _, string openApiSpec)
    {
        var sourceCode = GetSecuritySchemesSourceCode(openApiSpec);

        sourceCode.Should().Contain("internal const string SecretKeyKey = \"secret_key\";");
        sourceCode.Should().Contain("internal static class SecretKey");
        sourceCode.Should().Contain("TryGetParameter(HttpContext context, out", Exactly.Twice());
        sourceCode.Should().NotContain("GetParameter(HttpContext context)");
    }

    [Theory]
    [MemberData(nameof(ApiKeySecuritySchemeWithNoParameterSpecs))]
    public void GivenApiKeySecuritySchemeWithNoParameter_WhenGenerating_SecuritySchemesClassHasNameAndInConstants(
        string _, string openApiSpec)
    {
        var sourceCode = GetSecuritySchemesSourceCode(openApiSpec);

        sourceCode.Should().Contain("internal const string SecretKeyKey = \"secret_key\";");
        sourceCode.Should().Contain("internal static class SecretKey");
        sourceCode.Should().Contain("internal const string Name = \"X-API-Key\";");
        sourceCode.Should().Contain("internal const string In = \"header\";");
        sourceCode.Should().NotContain("GetParameter(HttpContext context)");
        sourceCode.Should().NotContain("TryGetParameter(HttpContext context, out");
    }

    [Theory]
    [MemberData(nameof(ApiKeySecuritySchemeWithMixedParameterSpecs))]
    public void GivenApiKeySecuritySchemeWithMixedParameters_WhenGenerating_SecuritySchemesClassHasBothNameInConstantsAndTryGetParameterMethod(
        string _, string openApiSpec)
    {
        var sourceCode = GetSecuritySchemesSourceCode(openApiSpec);

        sourceCode.Should().Contain("internal const string SecretKeyKey = \"secret_key\";");
        sourceCode.Should().Contain("internal static class SecretKey");
        sourceCode.Should().Contain("internal const string Name = \"X-API-Key\";");
        sourceCode.Should().Contain("internal const string In = \"header\";");
        sourceCode.Should().Contain("TryGetParameter(HttpContext context, out", Exactly.Once());
        sourceCode.Should().NotContain("GetParameter(HttpContext context)");
    }

    private string GetSecuritySchemesSourceCode(string openApiSpec)
    {
        var compilation = SetupGenerator(openApiSpec, out var diagnostics);
        HasOnlyMissingHandler(diagnostics);

        var securitySchemesSyntaxTree = compilation.SyntaxTrees
            .FirstOrDefault(t => Path.GetFileName(t.FilePath) == "SecuritySchemes.g.cs");
        securitySchemesSyntaxTree.Should().NotBeNull();

        return securitySchemesSyntaxTree!.ToString();
    }
}
