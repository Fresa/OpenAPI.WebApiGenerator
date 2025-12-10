using System.Collections.Generic;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseGenerator(List<ResponseContentGenerator> responseBodyGenerators, HttpResponseExtensionsGenerator httpResponseExtensionsGenerator)
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
                {
                    internal abstract void WriteTo(HttpResponse httpResponse);
                
                    {{responseBodyGenerators.AggregateToString(generator => 
                        generator.GenerateResponseContentClass())}}
                }
                #nullable restore
              """);
    }
}