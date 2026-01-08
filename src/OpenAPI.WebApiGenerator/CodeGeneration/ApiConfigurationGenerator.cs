namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ApiConfigurationGenerator(string @namespace)
{
    private const string ClassName = "WebApiConfiguration";
    
    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using System;
        
        namespace {{@namespace}};
              
        public sealed class {{ClassName}} 
        {
            /// <summary>
            /// The url to the OpenAPI specification used to generate the API 
            /// </summary>
            public Uri? OpenApiSpecification { get; set; }
        }
        #nullable restore
        """);
}