using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.SourceGeneratorTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.CodeGeneration;
using OpenAPI.WebApiGenerator.Extensions;
using OpenAPI.WebApiGenerator.OpenApi;
using JsonPointer = Corvus.Json.JsonPointer;

namespace OpenAPI.WebApiGenerator;

[Generator]
public sealed class ApiGenerator : IIncrementalGenerator
{
    private static readonly IDocumentResolver MetaSchemaResolver = SourceGeneratorHelpers.CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = SourceGeneratorHelpers.CreateVocabularyRegistry(MetaSchemaResolver);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Debugger.Launch();

        var provider = context.AdditionalTextsProvider
            .Where(additionalText => Path.GetFileName(additionalText.Path).EndsWith(".json"))
            .Select((text, _) => OpenApiDocument.Load(text.AsStream(), "json").Document ?? throw new InvalidOperationException($"Could not load OpenAPI document {text.Path}"))
            .Collect();
        
        var openapiDocumentProvider = provider.Select((array, _) => array.First());
        
        // Get global options
        var globalOptions =
            context.AnalyzerConfigOptionsProvider.Select((optionsProvider, token) =>
                new SourceGeneratorHelpers.GlobalOptions(
                    fallbackVocabulary: Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
                    optionalAsNullable: true,
                    useOptionalNameHeuristics: true,
                    alwaysAssertFormat: true,
                    ImmutableArray<string>.Empty));

        var openApiProvider = globalOptions
            .Combine(openapiDocumentProvider)
            .Combine(context.CompilationProvider)
            .Select((tuple, _) => (
                Options: tuple.Left.Left,
                OpenApiDocument: tuple.Left.Right,
                Compilation: tuple.Right
            ));

        context.RegisterSourceOutput(openApiProvider,
            WithExceptionReporting<(SourceGeneratorHelpers.GlobalOptions, OpenApiDocument, Compilation)>(GenerateCode));
    }

    private static void GenerateCode(SourceProductionContext context, (
        SourceGeneratorHelpers.GlobalOptions Options, 
        OpenApiDocument OpenApiDocument, 
        Compilation Compilation) generatorContext)
    {
        var globalOptions = generatorContext.Options;
        var compilation = generatorContext.Compilation;
        var endpointGenerator = new OperationGenerator(compilation);
        var rootNamespace = compilation.Assembly.Name;

        var openApi = generatorContext.OpenApiDocument;
        var openApiSpecAsJson = GetOpenApiSpecAsJson(openApi);
        var openApiUri = "http://test.com/test.json";
        //var openApiSpecSource = new InMemoryAdditionalText("http://test.com/test.json", openApiSpecAsJson);
        var documentResolver = new PrepopulatedDocumentResolver();
        documentResolver.AddDocument(openApiUri, JsonDocument.Parse(openApiSpecAsJson));
        // SourceGeneratorHelpers.BuildDocumentResolver([openApiSpecSource], context.CancellationToken);
        var generationContext = new SourceGeneratorHelpers.GenerationContext(documentResolver, globalOptions);
        var openApiVisitor = OpenApiPointerVisitor.V3(JsonNode.Parse(openApiSpecAsJson) ??
                                 throw new InvalidOperationException("OpenApi spec is empty"));

        // var visit = new OpenApiWalker(new OpenApiJsonPointerVisitor());
        // visit.Walk(openApi);
        var httpRequestExtensionsGenerator = new HttpRequestExtensionsGenerator(rootNamespace);
        var httpRequestExtensionSourceCode =
            httpRequestExtensionsGenerator.GenerateHttpRequestExtensionsClass();
        httpRequestExtensionSourceCode.AddTo(context);
        
        var httpResponseExtensionsGenerator = new HttpResponseExtensionsGenerator(rootNamespace);
        var httpResponseExtensionSourceCode =
            httpResponseExtensionsGenerator.GenerateHttpResponseExtensionsClass();
        httpResponseExtensionSourceCode.AddTo(context);
        
        var operations = new List<(string Namespace, HttpMethod HttpMethod)>();
        
        using var pathsPointer = openApiVisitor.Visit(openApi.Paths);
        foreach (var path in openApi.Paths)
        {
            using var pathPointer = openApiVisitor.Visit(path);
            var pathExpression = path.Key;
            var pathItem = path.Value;
            var entityType = pathExpression.ToPascalCase();
            var entityNamespace = $"{rootNamespace}.{entityType}";
            var entityDirectory = entityType;
            var parameterGenerators = new Dictionary<string, ParameterGenerator>();
            using var parametersPointer = openApiVisitor.Visit(pathItem.Parameters);
            foreach (var (parameter, i) in (pathItem.Parameters ?? []).WithIndex())
            {
                using var parameterPointer = openApiVisitor.Visit(parameter, i);
                // var schema = new InMemoryAdditionalText(
                //     $"/{entityDirectory}/{parameter.GetTypeDeclarationIdentifier()}.json",
                //     parameter.GetSchema().SerializeToJson());
                using var schemaPointer = openApiVisitor.VisitSchema(parameter);
                var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                    ns: entityNamespace,
                    typeName: Path.Combine(entityDirectory, parameter.GetTypeDeclarationIdentifier()),
                    // location: schema.Path,
                    location: openApiUri + "#"+  openApiVisitor.GetPointer(),
                    // location:  openApiUri + "#/parameters/AllowTrailingDot",
                    rebaseToRootPath: false);
                //var typeDeclaration = GenerateCode(context, generationSpecification, schema, globalOptions);
                var typeDeclaration = GenerateCode(context, generationSpecification, generationContext, globalOptions);
                parameterGenerators[parameter.GetName()] = new ParameterGenerator(typeDeclaration, parameter,
                    httpRequestExtensionsGenerator);
            }

            foreach (var openApiOperation in path.Value.GetOperations())
            {
                var operationMethod = openApiOperation.Key;
                var operation = openApiOperation.Value;
                var operationId = (operation.OperationId ?? operationMethod.ToString()).ToPascalCase();
                var operationNamespace = $"{entityNamespace}.{operationId}";
                var operationDirectory = $"{entityDirectory}/{operationId}";

                foreach (var parameter in operation.GetParameters())
                {
                    var schema = new InMemoryAdditionalText(
                        $"/{operationDirectory}/{parameter.GetTypeDeclarationIdentifier()}.json",
                        parameter.GetSchema().SerializeToJson());

                    var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                        ns: operationNamespace,
                        typeName: Path.Combine(operationDirectory, parameter.GetTypeDeclarationIdentifier()),
                        location: schema.Path,
                        rebaseToRootPath: false);

                    var typeDeclaration = GenerateCode(context, generationSpecification, schema, globalOptions);
                    parameterGenerators[parameter.GetName()] = new ParameterGenerator(typeDeclaration, parameter,
                        httpRequestExtensionsGenerator);
                }

                var requestBodyNamespace = $"{operationNamespace}.Requests";
                var requestBodyDirectory = Path.Combine(operationDirectory, "Requests");
                var body = operation.RequestBody;
                var requestBodyGenerator = RequestBodyGenerator.Empty;
                if (body is not null)
                {
                    var contentGenerators = body.GetContent().Select(pair =>
                    {
                        var requestBodyContent = pair.Value;
                        var bodyTypeDeclarationIdentifier = pair.Key.ToPascalCase();

                        var schema = new InMemoryAdditionalText(
                            $"/{requestBodyDirectory}/{bodyTypeDeclarationIdentifier}.json",
                            requestBodyContent.Schema.SerializeToJson());

                        var contentSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                            ns: requestBodyNamespace,
                            typeName: Path.Combine(requestBodyDirectory, bodyTypeDeclarationIdentifier),
                            location: schema.Path,
                            rebaseToRootPath: false);

                        var typeDeclaration = GenerateCode(context, contentSpecification, schema, globalOptions);
                        return new RequestBodyContentGenerator(
                            pair.Key,
                            typeDeclaration,
                            httpRequestExtensionsGenerator);
                    }).ToList();
                    requestBodyGenerator = new RequestBodyGenerator(
                        body,
                        contentGenerators);
                }

                var requestGenerator =
                    new RequestGenerator(parameterGenerators.Values.ToList(), requestBodyGenerator);
                var requestSourceCode = requestGenerator.GenerateRequestClass(
                    operationNamespace,
                    operationDirectory);
                requestSourceCode.AddTo(context);

                var responseContentNamespace = operationNamespace + ".Responses";
                var responseContentDirectory = Path.Combine(operationDirectory, "Responses");
                var responses = operation.Responses ?? new OpenApiResponses
                {
                    ["default"] = new OpenApiResponse()
                };
                var responseBodyGenerators = responses.Select(pair =>
                {
                    var response = pair.Value;
                    var responseStatusCodePattern = pair.Key.ToPascalCase();

                    var responseContent = response.Content ?? new Dictionary<string, OpenApiMediaType>
                    {
                        // Any content
                        ["*/*"] = new()
                    };
                    var responseBodyGenerators = responseContent.Select(valuePair =>
                    {
                        var content = valuePair.Value;
                        var contentType = valuePair.Key.ToPascalCase();
                        var schema = new InMemoryAdditionalText(
                            $"/{responseContentDirectory}/{responseStatusCodePattern}/{contentType}.json",
                            content.Schema.SerializeToJson());

                        var contentSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                            ns: $"{responseContentNamespace}._{responseStatusCodePattern}",
                            typeName: Path.Combine(responseContentDirectory, responseStatusCodePattern,
                                contentType),
                            location: schema.Path,
                            rebaseToRootPath: false);

                        var typeDeclaration = GenerateCode(context, contentSpecification, schema, globalOptions);
                        return new ResponseBodyContentGenerator(valuePair.Key, typeDeclaration);
                    }).ToList();

                    var responseHeaderGenerators = response.Headers?.Select(valuePair =>
                    {
                        var name = valuePair.Key;
                        var typeName = name.ToPascalCase();
                        var header = valuePair.Value;
                        var schema = new InMemoryAdditionalText(
                            $"/{responseContentDirectory}/{responseStatusCodePattern}/Headers/{typeName}.json",
                            header.GetSchema().SerializeToJson());

                        var headerSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                            ns: $"{responseContentNamespace}._{responseStatusCodePattern}.Headers",
                            typeName: Path.Combine(responseContentDirectory, responseStatusCodePattern, "Headers",
                                typeName),
                            location: schema.Path,
                            rebaseToRootPath: false);

                        var typeDeclaration = GenerateCode(context, headerSpecification, schema, globalOptions);
                        return new ResponseHeaderGenerator(name, header, typeDeclaration,
                            httpResponseExtensionsGenerator);
                    }).ToList() ?? [];

                    return new ResponseContentGenerator(
                        responseStatusCodePattern,
                        responseBodyGenerators,
                        responseHeaderGenerators,
                        httpResponseExtensionsGenerator);
                }).ToList();
                var responseGenerator = new ResponseGenerator(
                    responseBodyGenerators, httpResponseExtensionsGenerator);
                var responseSourceCode =
                    responseGenerator.GenerateResponseClass(
                        operationNamespace,
                        operationDirectory);
                responseSourceCode.AddTo(context);

                operations.Add((operationNamespace, operationMethod));
                var endpointSource = endpointGenerator
                    .Generate(operationNamespace,
                        operationDirectory,
                        pathExpression,
                        operationMethod);
                endpointSource
                    .AddTo(context);
            }
        }


        if (endpointGenerator.TryGenerateMissingHandlers(out var missingHandlers))
        {
            foreach (var missingHandler in missingHandlers)
            {
                missingHandler.SourceCode.AddTo(context);
                context.ReportDiagnostic(missingHandler.Diagnostic);
            }
        }

        var operationRouterGenerator = new OperationRouterGenerator(rootNamespace);
        var routerSourceCode = operationRouterGenerator.ForMinimalApi(operations);
        routerSourceCode.AddTo(context);
    }

    private static readonly DiagnosticDescriptor Crv1001ErrorGeneratingCSharpCode =
        new(
            id: "CRV1001",
            title: "JSON Schema Type Generator Error",
            messageFormat: "Error generating C# code: {0}: {1}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    private static TypeDeclaration GenerateCode(SourceProductionContext context,
        SourceGeneratorHelpers.GenerationSpecification specification,
        SourceGeneratorHelpers.GenerationContext generationContext,
        SourceGeneratorHelpers.GlobalOptions globalOptions)
    {
        var typeDeclarations = GenerateCode(context, new SourceGeneratorHelpers.TypesToGenerate(
            [specification], generationContext), VocabularyRegistry);
        return typeDeclarations.Single();
    }
    
    private static TypeDeclaration GenerateCode(SourceProductionContext context,
        SourceGeneratorHelpers.GenerationSpecification specification,
        AdditionalText schema,
        SourceGeneratorHelpers.GlobalOptions globalOptions)
    {
        var generationContext = new SourceGeneratorHelpers.GenerationContext(SourceGeneratorHelpers.BuildDocumentResolver([schema], context.CancellationToken), globalOptions);
        var typeDeclarations = GenerateCode(context, new SourceGeneratorHelpers.TypesToGenerate(
            [specification], generationContext), VocabularyRegistry);
        return typeDeclarations.Single();
    }

    private static List<TypeDeclaration> GenerateCode(SourceProductionContext context, SourceGeneratorHelpers.TypesToGenerate typesToGenerate, VocabularyRegistry vocabularyRegistry)
    {
        if (typesToGenerate.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return [];
        }

        List<TypeDeclaration> typeDeclarationsToGenerate = [];
        Dictionary<string, string> namespaceToPathConversion = [];
        List<CSharpLanguageProvider.NamedType> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(typesToGenerate.DocumentResolver, vocabularyRegistry);

        string? defaultNamespace = null;

        foreach (SourceGeneratorHelpers.GenerationSpecification spec in typesToGenerate.GenerationSpecifications)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return [];
            }

            string schemaFile = spec.Location;
            JsonReference reference = new(schemaFile);
            TypeDeclaration rootType;
            try
            {
                rootType = typeBuilder.AddTypeDeclarations(reference, typesToGenerate.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Crv1001ErrorGeneratingCSharpCode,
                        Location.None,
                        reference,
                        ex.Message));

                return [];
            }
            
            typeDeclarationsToGenerate.Add(rootType);

            defaultNamespace ??= spec.Namespace;

            if (string.IsNullOrEmpty(spec.TypeName))
            {
                throw new InvalidOperationException($"Missing type name for schema {spec.Location}");
            }

            // Corvus doesn't support defining paths for the source code file hint, so we piggyback such information on the type name property 
            var filePath = Path.GetDirectoryName(spec.TypeName!);
            if (filePath == string.Empty)
            {
                throw new InvalidOperationException($"Expected type {spec.TypeName} to contain a path");
            }
            var typeName = Path.GetFileName(spec.TypeName!);
            
            namedTypes.Add(
                new CSharpLanguageProvider.NamedType(
                    rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                    typeName,
                    spec.Namespace,
                    spec.Accessibility));
            namespaceToPathConversion[spec.Namespace] = filePath;
        }

        CSharpLanguageProvider.Options options = new(
            defaultNamespace ?? "GeneratedTypes",
            [.. namedTypes],
            useOptionalNameHeuristics: typesToGenerate.UseOptionalNameHeuristics,
            alwaysAssertFormat: typesToGenerate.AlwaysAssertFormat,
            optionalAsNullable: typesToGenerate.OptionalAsNullable,
            disabledNamingHeuristics: [.. typesToGenerate.DisabledNamingHeuristics],
            fileExtension: ".g.cs",
            defaultAccessibility: typesToGenerate.DefaultAccessibility);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        IReadOnlyCollection<GeneratedCodeFile> generatedCode;

        try
        {
            generatedCode =
                typeBuilder.GenerateCodeUsing(
                    languageProvider,
                    context.CancellationToken,
                    typeDeclarationsToGenerate);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Crv1001ErrorGeneratingCSharpCode,
                    Location.None,
                    ex.Message));

            return [];
        }

        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                var filePath = namespaceToPathConversion[codeFile.TypeDeclaration.DotnetNamespace()];
                var fileName = Path.Combine(filePath, codeFile.FileName);
                
                var sourceCode = new SourceCode(
                    fileName,
                    codeFile.FileContent
                );
                sourceCode.AddTo(context);
            }
        }

        return typeDeclarationsToGenerate
            .Select(declaration => declaration.ReducedTypeDeclaration().ReducedType)
            .ToList();
    }
    
    private static Action<SourceProductionContext, T> WithExceptionReporting<T>(
        Action<SourceProductionContext, T> handler) =>
        (productionContext, input) =>
        {
            try
            {
                handler.Invoke(productionContext, input);
            }
            catch (Exception e)
            {
                var stackTrace = new StackTrace(e, true);
                StackFrame? firstFrameWithLineNumber = null;
                for (var i = 0; i < stackTrace.FrameCount; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    if (frame.GetFileLineNumber() != 0)
                    {
                        firstFrameWithLineNumber = frame;
                        break;
                    }
                }

                var firstStackTraceLocation = firstFrameWithLineNumber == null ?
                    Location.None :
                    Location.Create(
                        firstFrameWithLineNumber.GetFileName(),
                        new TextSpan(),
                        new LinePositionSpan(
                            new LinePosition(
                                firstFrameWithLineNumber.GetFileLineNumber(),
                                firstFrameWithLineNumber.GetFileColumnNumber()),
                            new LinePosition(
                                firstFrameWithLineNumber.GetFileLineNumber(),
                                firstFrameWithLineNumber.GetFileColumnNumber() + 1)));

                productionContext.ReportDiagnostic(Diagnostic.Create(
                    UnhandledException,
                    location: firstStackTraceLocation,
                    // Only single line https://github.com/dotnet/roslyn/issues/1455
                    messageArgs: [e.ToString().Replace("\r\n", " |").Replace("\n", " |")]));
            }
        };
    
    private static readonly DiagnosticDescriptor UnhandledException =
        new(
            id: "AF0001",
            title: "Unhandled error",
            // Only single line https://github.com/dotnet/roslyn/issues/1455
            messageFormat: "{0}",
            category: "Compiler",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            // Doesn't work
            description: null,
            customTags: WellKnownDiagnosticTags.AnalyzerException);

    private static string GetOpenApiSpecAsJson(OpenApiDocument openApi)
    {
        var textWriter = new StringWriter();
        using (textWriter)
        {
            var jsonWriter = new OpenApiJsonWriter(textWriter);
            openApi.SerializeAsV2(jsonWriter);
        }

        return textWriter.GetStringBuilder().ToString();
    }
}