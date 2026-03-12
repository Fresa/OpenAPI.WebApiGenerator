using System.Net.Http.Headers;
using AwesomeAssertions;
using OpenAPI.WebApiGenerator.Extensions;
using Xunit;

namespace OpenAPI.WebApiGenerator.UnitTests.Extensions;

public class MediaTypeExtensionsTests
{
    [Theory]
    [InlineData("*/*", "true")]
    [InlineData("*/*; charset=utf-8", """true && test.Parameters.Contains(new NameValueHeaderValue("charset", "utf-8"))""")]
    [InlineData("text/*", """test.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)""")]
    [InlineData("text/*; charset=utf-8", """test.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) && test.Parameters.Contains(new NameValueHeaderValue("charset", "utf-8"))""")]
    [InlineData("application/json", """test.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)""")]
    [InlineData("application/json; charset=utf-8", """test.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) && test.Parameters.Contains(new NameValueHeaderValue("charset", "utf-8"))""")]
    [InlineData("application/json; charset=utf-8; boundary=something", """test.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) && test.Parameters.Contains(new NameValueHeaderValue("charset", "utf-8")) && test.Parameters.Contains(new NameValueHeaderValue("boundary", "something"))""")]
    [InlineData("multipart/form-data; boundary=something", """test.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) && test.Parameters.Contains(new NameValueHeaderValue("boundary", "something"))""")]
    [InlineData("multipart/form-data; boundary", """test.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase) && test.Parameters.Contains(new NameValueHeaderValue("boundary"))""")]
    public void MediaTypeHeaderValue_MatchConditionExpressions(string mediaTypeValue, string expectedExpression)
    {
        var mediaType = MediaTypeHeaderValue.Parse(mediaTypeValue);
        var expression = mediaType.GetMatchConditionExpression("test");
        expression.Should().Be(expectedExpression);
    }

    [Theory]
    [InlineData("*/*", 0)]
    [InlineData("text/*", 100)]
    [InlineData("application/*; charset=utf-8", 101)]
    [InlineData("application/*; charset=utf-8; boundary=something", 102)]
    [InlineData("application/*; charset=utf-8; boundary=something; foo=bar", 103)]
    [InlineData("application/json", 1000)]
    [InlineData("application/json; charset=utf-8", 1001)]
    [InlineData("application/json; charset=utf-8; boundary=something", 1002)]
    [InlineData("multipart/form-data; boundary", 1001)]
    public void MediaTypeHeaderValue_Precedence(string mediaTypeValue, int expectedPrecedence)
    {
        var mediaType = MediaTypeHeaderValue.Parse(mediaTypeValue);
        var precedence = mediaType.GetPrecedence();
        precedence.Should().Be(expectedPrecedence);
    }
}