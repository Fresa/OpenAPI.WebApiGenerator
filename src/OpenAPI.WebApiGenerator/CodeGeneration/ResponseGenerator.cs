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
#nullable restore
""");
    }
}