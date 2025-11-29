# OpenApi.WebApiGenerator

Generates scaffolding for Web APIs from OpenAPI specifications. 

The generated functionality will route, serialize/deserialize and validate payloads according to the specification.

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
    <PackageReference Include="WebApiGenerator.OpenAPITest" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```
2. Add a reference to your OpenAPI specification:
```
<ItemGroup>
    <AdditionalFiles Include="path/to/OpenAPI_Specification.json"/>
</ItemGroup>
```
3. Add references to [Corvus.Json.ExtendedTypes](https://github.com/corvus-dotnet/Corvus.JsonSchema?tab=readme-ov-file#corvusjsonextendedtypes) and [ParameterStyleParsers.OpenAPI](https://github.com/Fresa/OpenAPI.ParameterStyleParsers). 
```
<ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.3.13" />
    <PackageReference Include="ParameterStyleParsers.OpenAPI" Version="1.1.0" />
</ItemGroup>
```
* Corvus.Json.ExtendedTypes >= 4.0.0
* ParameterStyleParsers.OpenAPI >= 1.1.0

4. Compile the project.


5. Register API operations in `Program.cs`.
```
var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();
var app = builder.Build();
app.MapOperations();
app.Run();
```

See [Example.Api](tests/Example.Api) as an example.

**Note**: The Example.Api references the generator through a project reference. Use a package reference instead as described above.  

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
## Dependency Injection
Operations are registered as scoped dependencies. Any dependencies can be injected into them as usual via the app builder's `IServiceCollection`. 

# Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

# License
[MIT](LICENSE)