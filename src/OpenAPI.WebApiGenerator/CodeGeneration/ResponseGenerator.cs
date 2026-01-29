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

internal abstract partial class Response
{{{Enumerable.Range(1, 5).AggregateToString(i => 
$$"""
    protected int Validate{{i}}xxStatusCode(int code) 
        => (code >= {{i}}00 && code <= {{i}}99) ? code : throw new InvalidOperationException($"Expected {{i}}xx status code, got {code}");
""")}}
    
    internal abstract void WriteTo(HttpResponse httpResponse);
    internal abstract ValidationContext Validate(ValidationLevel validationLevel);
    
    internal sealed class Unauthorized : Response
    {
        internal override void WriteTo(HttpResponse httpResponse)
        {
            httpResponse.StatusCode = 401;
        }

        internal override ValidationContext Validate(ValidationLevel validationLevel) => ValidationContext.ValidContext; 
    }
    
    internal sealed class Forbidden : Response
    {
        internal override void WriteTo(HttpResponse httpResponse)
        {
            httpResponse.StatusCode = 403;
        }

        internal override ValidationContext Validate(ValidationLevel validationLevel) => ValidationContext.ValidContext; 
    }
    {{
    responseBodyGenerators.AggregateToString(generator => 
        generator.GenerateResponseContentClass()).Indent(4)
    }}
}
#nullable restore
""");
    }
}