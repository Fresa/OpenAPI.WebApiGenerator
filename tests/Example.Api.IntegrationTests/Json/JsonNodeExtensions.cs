using System.Text.Json.Nodes;
using AwesomeAssertions;
using Json.Pointer;

namespace Example.Api.IntegrationTests.Json;

internal static class JsonNodeExtensions
{
    internal static JsonNode Evaluate(this JsonNode? node, string path)
    {
        JsonPointer.Parse(path).TryEvaluate(node, out var value).Should()
            .BeTrue($"because the json node should contain the property {path}");
        value.Should().NotBeNull($"because the property {path} should not be null");
        return value!;
    }

    internal static T GetValue<T>(this JsonNode? node, string path)
    {
        var value = node.Evaluate(path);
        return value.GetValue<T>();
    }

}