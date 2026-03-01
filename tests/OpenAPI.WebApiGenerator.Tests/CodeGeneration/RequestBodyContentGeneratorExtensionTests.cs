using System.Linq;
using AwesomeAssertions;
using OpenAPI.WebApiGenerator.CodeGeneration;
using Xunit;

namespace OpenAPI.WebApiGenerator.Tests.CodeGeneration;

public class RequestBodyContentGeneratorExtensionTests
{
    [Fact]
    public void ListOfRequestBodyContentGenerators_SortByContentType_SortsAccordingToPrecedence()
    {
        var generators = new[]
        {
            CreateGenerator("*/*"),
            CreateGenerator("application/json; q=0.5"),
            CreateGenerator("text/*"),
            CreateGenerator("application/json"),
            CreateGenerator("text/*; q=0.9"),
            CreateGenerator("application/json; charset=utf-8"),
            CreateGenerator("text/plain"),
        };

        var sorted = generators.SortByContentType()
            .Select(generator => generator.ContentType.ToString())
            .ToArray();

        sorted.Should().ContainInOrder(
            "application/json; charset=utf-8",
            "application/json",
            "text/plain",
            "text/*",
            "*/*",
            "text/*; q=0.9",
            "application/json; q=0.5");
    }

    private static RequestBodyContentGenerator CreateGenerator(string contentType) =>
        new(contentType, null!, null!);
}