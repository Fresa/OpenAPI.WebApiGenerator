using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseGenerator(
    List<ResponseContentGenerator> responseBodyGenerators, 
    HttpResponseExtensionsGenerator httpResponseExtensionsGenerator)
{
    public SourceCode GenerateResponseClass(string @namespace, string path)
    {
        return new SourceCode($"{path}/Response.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using Microsoft.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using {{httpResponseExtensionsGenerator.Namespace}};

namespace {{@namespace}};

/// <summary>
/// Contains the operation's response objects
/// </summary>
internal abstract partial class Response
{{{Enumerable.Range(1, 5).AggregateToString(i => 
$$"""
    /// <summary>
    /// Validate that status code is {{i}}xx
    /// <exception cref="InvalidOperationException">Thrown when the status code is not {{i}}xx</exception>
    /// </summary>
    /// <param name="code">Status code to validate</param>
    /// <returns>The validated status code</returns>
    protected int Validate{{i}}xxStatusCode(int code) 
        => (code >= {{i}}00 && code <= {{i}}99) ? code : throw new InvalidOperationException($"Expected {{i}}xx status code, got {code}");
""")}}
    
    /// <summary>
    /// Ensures that the specified content type matches the specification
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified content type does not match the specification</exception>
    /// </summary>
    /// <param name="contentType">Content type</param>
    /// <param name="expectedContentType">Expected content type</param>
    protected void EnsureExpectedContentType(MediaTypeHeaderValue contentType, MediaTypeHeaderValue expectedContentType)
    {
        if (!contentType.IsSubsetOf(expectedContentType))
        {
            throw new ArgumentOutOfRangeException($"Expected content type {contentType.MediaType} to be a subset of {expectedContentType.MediaType}");
        }
    }

    /// <summary>
    /// Write the response to a http response object
    /// </summary>
    /// <param name="httpResponse">Http response object to write the response to</param>
    internal abstract void WriteTo(HttpResponse httpResponse);
    
    /// <summary>
    /// Validate the response
    /// </summary>
    /// <param name="validationLevel">Validation level</param>
    /// <returns>The validation result</returns>
    internal abstract ValidationContext Validate(ValidationLevel validationLevel);
    {{
    responseBodyGenerators.AggregateToString(generator => 
        generator.GenerateResponseContentClass()).Indent(4)
    }}
}

/// <summary>
/// Represents a response with content
/// </summary>
/// <typeparam name="T">Response</typeparam>
internal interface IContent<T> where T : Response
{
    /// <summary>
    /// Contents for the response
    /// </summary>
    internal static abstract ContentMediaType<T>[] ContentMediaTypes { get; }
}

/// <summary>
/// Typed content media type
/// </summary>
/// <typeparam name="T">Response</typeparam>
internal readonly record struct ContentMediaType<T>(MediaTypeHeaderValue Value) 
    where T : Response
{
    /// <summary>
    /// Implicitly convert back to MediaTypeHeaderValue
    /// </summary>
    public static implicit operator MediaTypeHeaderValue(ContentMediaType<T> mediaType) => mediaType.Value;
}

internal partial class Request
{
    /// <summary>
    /// Returns the best response content media type match if an acceptable media type is found.
    /// </summary>
    /// <param name="matchedContentMediaType">Matched content media type if method returns true</param>
    /// <typeparam name="T">The response to match against</typeparam>
    /// <returns>True if a matched media type was found</returns>
    internal bool TryMatchAcceptMediaType<T>(
        [NotNullWhen(true)] out ContentMediaType<T>? matchedContentMediaType) where T : Response, IContent<T>
    {
        var mediaTypes = T.ContentMediaTypes;
        var acceptHeaders = HttpContext.Request.GetTypedHeaders().Accept;
        if (acceptHeaders is not { Count: > 0 })
        {
            matchedContentMediaType = mediaTypes.Length > 0 ? mediaTypes[0] : null;
            return matchedContentMediaType != null;
        }

        var sortedAcceptMediaTypes = acceptHeaders
            .OrderByDescending(headerValue => headerValue.Quality ?? 1.0)
            .ThenByDescending(headerValue => headerValue.MatchesAllTypes ? 0 : headerValue.MatchesAllSubTypes ? 1 : 2)
            .ThenByDescending(headerValue => headerValue.Parameters.Count);

        foreach (var acceptMediaType in sortedAcceptMediaTypes)
        {
            if ((acceptMediaType.Quality ?? 1.0) <= 0)
                continue;

            // Exact match
            var match = mediaTypes.FirstOrDefault(mediaType =>
                acceptMediaType.IsSubsetOf(mediaType.Value) && 
                mediaType.Value.IsSubsetOf(acceptMediaType));

            // Accept media type is broader than a supported media type;
            // */*, application/*, application/*+json -> matches Accept header */* 
            if (match.Value is null)
                match = mediaTypes.FirstOrDefault(mediaType => 
                    mediaType.Value.IsSubsetOf(acceptMediaType));

            // Accept media type fits within a broader supported media type;
            // Accept header application/json matches -> */*, application/* 
            if (match.Value is null)
                match = mediaTypes.FirstOrDefault(mediaType => 
                    acceptMediaType.IsSubsetOf(mediaType.Value));

            if (match.Value is not null)
            {
                matchedContentMediaType = match;
                return true;
            }
        }

        matchedContentMediaType = null;
        return false;
    } 
}
#nullable restore
""");
    }
}