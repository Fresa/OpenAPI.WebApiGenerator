# OpenApi.WebApiGenerator

Generates scaffolding for Web APIs from OpenAPI specifications. 

The generated functionality will route, serialize/deserialize and validate payloads according to the specification.

Supported OpenAPI version:
- [3.2.0](https://spec.openapis.org/oas/v3.2.0.html)
- [3.1.2](https://spec.openapis.org/oas/v3.1.2.html)
- [3.1.1](https://spec.openapis.org/oas/v3.1.1.html)
- [3.1.0](https://spec.openapis.org/oas/v3.1.0.html)
- [3.0.4](https://spec.openapis.org/oas/v3.0.4.html)
- [3.0.3](https://spec.openapis.org/oas/v3.0.3.html)
- [3.0.2](https://spec.openapis.org/oas/v3.0.2.html)
- [3.0.1](https://spec.openapis.org/oas/v3.0.1.html)
- [3.0.0](https://spec.openapis.org/oas/v3.0.0.html)
- [2.0](https://spec.openapis.org/oas/v2.0.html)

API frameworks supported:
- [Minimal API](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)

.NET versions supported:
- \>=9.0

## Installation
```Shell
dotnet add package WebApiGenerator.OpenAPI
```

https://www.nuget.org/packages/WebApiGenerator.OpenAPI

## Getting Started
1. Add a reference to the generator in the project file where the API should exist:
```
<ItemGroup>
    <PackageReference Include="WebApiGenerator.OpenAPI" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```
2. Add a reference to your OpenAPI specification:
```
<ItemGroup>
    <AdditionalFiles Include="path/to/openapi.json"/>
</ItemGroup>
```
The first file containing the word "openapi" and have an ending of .json, .yaml or .yml will be read.

Supported data formats are:
* JSON
* YAML

3. Add references to [Corvus.Json.ExtendedTypes](https://github.com/corvus-dotnet/Corvus.JsonSchema?tab=readme-ov-file#corvusjsonextendedtypes) and [ParameterStyleParsers.OpenAPI](https://github.com/Fresa/OpenAPI.ParameterStyleParsers). 
```
<ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.3.13" />
    <PackageReference Include="ParameterStyleParsers.OpenAPI" Version="1.4.0" />
</ItemGroup>
```
* Corvus.Json.ExtendedTypes >= 4.0.0
* ParameterStyleParsers.OpenAPI >= 1.4.0

4. Compile the project.


5. Register API operations in `Program.cs`.
```
var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();
var app = builder.Build();
app.MapOperations();
app.Run();
```

## Examples:
- [OpenAPI 2.0](tests/Example.OpenApi20)
- [OpenAPI 3.0](tests/Example.OpenApi30)
- [OpenAPI 3.1](tests/Example.OpenApi31)
- [OpenAPI 3.2](tests/Example.OpenApi32)

All specifications mostly generate similar abstractions. What might differ is the location of generated resources, which follows the respective structure of the OpenAPI specification, and the JSON types, which are based on the respective schema version. 

**Note**: The examples reference the generator through a project reference. Use a package reference instead as described above.  

## Implementing an [API Operation](https://swagger.io/specification/#operation-object)
The generator generates stubbed partial classes for any operation handlers (`Foo.Bar.Operation.Handler.cs`) if there are none existing in the project and logs it with a compiler warning (AF1001). The classes should be copied into source control and the operation methods implemented. The operation methods have a familiar request/response design:
```
internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken);
```

The generated stubbed operation handler classes can be copied either manually or automatically.
### Manually
Copy the content using the Solution Explorer in the IDE and create a proper file to paste it into:
- JetBrains Rider: `MyProject/Dependencies/.NET X.0/Source Generators/OpenAPI.Generator/Foo.Bar.Operation.Handler.cs`
- Visual Studio: `MyProject/Dependencies/Analyzers/OpenAPI.Generator/OpenAPI.Generator.OpenApiGenerator/Foo.Bar.Operation.Handler.cs`

### Automatically
Let the compiler output all generated files to a directory during compilation by adding these directives to the project:
```
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Make sure to not include the files outputted when compiling again:
```
<ItemGroup>
    <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**" />
</ItemGroup>
```

To copy the operation handlers add the following target:
```
<Target Name="CopyMissingOperationHandlers" 
        AfterTargets="Build" 
        Condition="'$(EmitCompilerGeneratedFiles)'=='true'">
    <ItemGroup>
        <TextFiles Include="$(CompilerGeneratedFilesOutputPath)\**\Operation.Handler.g.cs" />
    </ItemGroup>
    <Copy
        SourceFiles="@(TextFiles)"
        DestinationFiles="@(TextFiles->'generated-api-handlers\%(RecursiveDir)%(Filename)%(Extension)')"
        ContinueOnError="true" />
</Target>
```
Exchange `generated-api-handlers` to any directory. 

These handlers will not be generated in subsequent compilations as the generator will detect that they already exist, but the output directory should be cleaned before compiling to avoid the same files to be copied again (and overwrite any changes done):
```
<Target Name="CleanSourceGeneratedFiles"
        BeforeTargets="BeforeBuild"
        DependsOnTargets="$(BeforeBuildDependsOn)"
        Condition="'$(EmitCompilerGeneratedFiles)'=='true'">
    <RemoveDir Directories="$(CompilerGeneratedFilesOutputPath)" />
</Target>
```

## Content Negotiation
Content is negotiated for both request and responses.

See the [examples](#examples) for more details.
### Request Body Content
Request body content is automatically mapped via the [Content-Type](https://datatracker.ietf.org/doc/html/rfc9110#field.content-type) header. The `Request.Body` property has content properties generated for all specified content which can be tested for nullability to figure out which one was sent. 

If `Body` is optional, all content properties might be null.

If body is not defined for the request, there will be no `Body` property generated. 

### Response Content
Response content can be negotiated using the `TryMatchAcceptMediaType` method exposed by the `Request` class. Call it with the wanted response and it will return the best content matching the [Accept](https://datatracker.ietf.org/doc/html/rfc9110#name-accept) header.

This method can only be used with response that define content, and it is scoped to responses defined by the current operation.

Example:
```dotnet
switch (request.TryMatchAcceptMediaType<Response.OK200>(out var matchedMediaType))
{
    // No match, the server decides what to do
    case false:
    // Matched any application content (application/*)
    case true when matchedMediaType == Response.OK200.AnyApplication.ContentMediaType:
        return Task.FromResult<Response>(new Response.OK200.AnyApplication(
            Components.Schemas.FooProperties.Create(name: request.Body.ApplicationJson?.Name),
            "application/json") { Headers = new Response.OK200.ResponseHeaders { Status = 2 } });
    // Matched content that has not been implemented yet by the operation handler (can be used to detect newly specified content that has not yet been implemented)
    default:
        throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
} 
```

## Sequential Media Types
OpenAPI 3.2 added support for [sequential media types](https://spec.openapis.org/oas/v3.2.0.html#sequential-media-types). The following sequential media types are supported for both request and response media content:
- application/jsonl
- application/x-ndjson
- application/x-jsonlines
- application/json-seq
- application/geo+json-seq

Other sequential media types can be implemented by simply following the expected naming convention and placing the implementations in the expected namespace, see the compilation error of any missing media type class.

### Request Content 
Inherit from `SequentialJsonEnumerable<T>` using the following naming convention:
- application/jsonl -> `ApplicationJsonlEnumerable<T>`

### Response Content
Inherit from `SequentialJsonWriter<T>` using the following naming convention:
- application/jsonl -> `ApplicationJsonlWriter<T>`

See the [OpenAPI 3.2 examples](#examples) for further details how to consume and produce sequential media types.

## Authentication and Authorization
OpenAPI defines [security scheme objects](https://spec.openapis.org/oas/latest#security-scheme-object) for authentication and authorization mechanisms. The generator implement endpoint filters that corresponds to the security declaration of each operation. Do _not_ call `UseAuthentication` or similar when configuring the application.

The security schemes for the [security requirements](https://spec.openapis.org/oas/latest#security-requirement-object) declared by the operations must be implemented. Use the familiar `AddAuthentication` builder method to register each scheme. Security scheme object configurations are generated to the `SecuritySchemes` class and can be used to configure the scheme implementations.

## Dependency Injection
Operations are registered as scoped dependencies. Any dependencies can be injected into them as usual via the app builder's `IServiceCollection`. 

## Options
To configure the generator add a configuration file:
```
<ItemGroup>
    <AdditionalFiles Include="path/to/OpenAPI.WebApiGenerator.json"/>
</ItemGroup>
```
Supported configuration:
* ValidationLevel  
Description: Sets global validation level  
Values: Flag|Basic|Detailed|Verbose  
Default: Detailed

Example:
```
{
    "ValidationLevel": "Verbose"
}
```

# Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

# License
[MIT](LICENSE)