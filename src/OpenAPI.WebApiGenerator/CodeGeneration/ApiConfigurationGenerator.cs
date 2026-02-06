namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ApiConfigurationGenerator(string @namespace, AuthGenerator authGenerator)
{
    private const string ClassName = "WebApiConfiguration";
    
    internal SourceCode GenerateClass() =>
        new($"{ClassName}.g.cs",
        $$"""
        #nullable enable
        using Microsoft.AspNetCore.Authorization;
        using System;
        
        namespace {{@namespace}};
              
        public sealed class {{ClassName}} 
        {
            /// <summary>
            /// The uri to the exposed OpenAPI specification used to generate the API.
            /// This is used in the SchemaLocation of the ValidationResult.
            /// <example>https://localhost/openapi.json</example> 
            /// </summary>
            public Uri? OpenApiSpecificationUri { get; init; }{{(authGenerator.HasSecuritySchemes ? 
        """
            
            internal SecuritySchemeOptions SecuritySchemeOptions { get; set; } = new();
        """ : "")}}
        }
        #nullable restore
        """);
}