using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
using OpenAPI.WebApiGenerator.OpenApi.Visitor;

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
            WithExceptionReporting<(SourceGeneratorHelpers.GlobalOptions, AdditionalText, Compilation)>(GenerateCode));
    }

    private static void GenerateCode(SourceProductionContext context, (
        SourceGeneratorHelpers.GlobalOptions Options, 
        AdditionalText OpenApiDocument, 
        Compilation Compilation) generatorContext)
    {
        var globalOptions = generatorContext.Options;
        var compilation = generatorContext.Compilation;
        var rootNamespace = compilation.Assembly.Name;
        var openApiDocumentFile = generatorContext.OpenApiDocument;
        var jsonValidationExceptionGenerator = new JsonValidationExceptionGenerator(rootNamespace);
        var jsonValidationExceptionSourceCode =
            jsonValidationExceptionGenerator.GenerateJsonValidationExceptionClass();
        jsonValidationExceptionSourceCode.AddTo(context);

        var endpointGenerator = new OperationGenerator(compilation, jsonValidationExceptionGenerator);
        var openApi = OpenApiDocument.Load(openApiDocumentFile.AsStream(), "json").Document ??
                      throw new InvalidOperationException(
                          $"Could not load OpenAPI document {openApiDocumentFile.Path}");
        var openApiUri = new JsonReference("http://test.com/test.json");
        var documentResolver = new PrepopulatedDocumentResolver();
        var openApiDocument = JsonDocument.Parse(generatorContext.OpenApiDocument.AsStream());
        if (!documentResolver.AddDocument(openApiUri, openApiDocument))
        {
            throw new InvalidOperationException("Could not add OpenApi document");
        }
        var generationContext = new SourceGeneratorHelpers.GenerationContext(documentResolver, globalOptions);
        var openApiReference = new OpenApiReference<OpenApiDocument>(openApi, openApiDocument, openApiUri);
        var openApiVisitor = OpenApiVisitor.V2(openApiReference);

        var httpRequestExtensionsGenerator = new HttpRequestExtensionsGenerator(rootNamespace);
        var httpRequestExtensionSourceCode =
            httpRequestExtensionsGenerator.GenerateHttpRequestExtensionsClass();
        httpRequestExtensionSourceCode.AddTo(context);
        
        var httpResponseExtensionsGenerator = new HttpResponseExtensionsGenerator(rootNamespace);
        var httpResponseExtensionSourceCode =
            httpResponseExtensionsGenerator.GenerateHttpResponseExtensionsClass();
        httpResponseExtensionSourceCode.AddTo(context);

        var operations = new List<(string Namespace, HttpMethod HttpMethod)>();
        
        foreach (var path in openApi.Paths)
        {
            var pathExpression = path.Key;
            var pathItem = path.Value;
            var openApiPathVisitor = openApiVisitor.Visit(pathItem);
            var entityType = pathExpression.ToPascalCase();
            var entityNamespace = $"{rootNamespace}.{entityType}";
            var entityDirectory = entityType;
            var pathParameterGenerators = new Dictionary<string, ParameterGenerator>();
            foreach (var parameter in pathItem.Parameters ?? [])
            {
                var schemaReference = openApiPathVisitor.GetSchemaReference(parameter);
                var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                    ns: entityNamespace,
                    typeName: Path.Combine(entityDirectory, parameter.GetTypeDeclarationIdentifier()),
                    location: schemaReference,
                    rebaseToRootPath: false);
                var typeDeclaration = GenerateCode(context, generationSpecification, generationContext, globalOptions);
                pathParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] = new ParameterGenerator(typeDeclaration, parameter,
                    httpRequestExtensionsGenerator);
            }

            foreach (var openApiOperation in path.Value.GetOperations())
            {
                var openApiOperationVisitor = openApiPathVisitor.Visit(openApiOperation.Key);
                var operationMethod = openApiOperation.Key;
                var operation = openApiOperation.Value;
                var operationId = (operation.OperationId ?? operationMethod.ToString()).ToPascalCase();
                var operationNamespace = $"{entityNamespace}.{operationId}";
                var operationDirectory = $"{entityDirectory}/{operationId}";
                var operationParameterGenerators = new Dictionary<string, ParameterGenerator>(pathParameterGenerators);

                foreach (var parameter in operation.GetParameters())
                {
                    var schemaReference = openApiOperationVisitor.GetSchemaReference(parameter);
                    var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                        ns: operationNamespace,
                        typeName: Path.Combine(operationDirectory, parameter.GetTypeDeclarationIdentifier()),
                        location: schemaReference,
                        rebaseToRootPath: false);

                    var typeDeclaration = GenerateCode(context, generationSpecification, generationContext, globalOptions);
                    operationParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] = new ParameterGenerator(typeDeclaration, parameter,
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
                        var schemaReference = openApiOperationVisitor.GetSchemaReference(requestBodyContent);
                        
                        var contentSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                            ns: requestBodyNamespace,
                            typeName: Path.Combine(requestBodyDirectory, bodyTypeDeclarationIdentifier),
                            location: schemaReference,
                            rebaseToRootPath: false);

                        var typeDeclaration = GenerateCode(context, contentSpecification, generationContext, globalOptions);
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
                    new RequestGenerator(operationParameterGenerators.Values.ToList(), requestBodyGenerator);
                var requestSourceCode = requestGenerator.GenerateRequestClass(
                    operationNamespace,
                    operationDirectory);
                requestSourceCode.AddTo(context);

                var responseContentNamespace = operationNamespace + ".Responses";
                var responseContentDirectory = Path.Combine(operationDirectory, "Responses");
                var responses = operation.Responses ??
                                throw new InvalidOperationException(
                                    $"No responses defined for operation {operationId}");
                var responseBodyGenerators = responses.Select(pair =>
                {
                    var response = pair.Value;
                    var responseStatusCodePattern = pair.Key.ToPascalCase();
                    var openApiResponseVisitor = openApiOperationVisitor.Visit(response);
                    var responseContent =
                        // OpenAPI.NET is incorrectly adding content when there is none defined. 
                        // No content definition means NO content.
                        (openApiResponseVisitor.HasContent() ? response.Content : null) ??
                        new Dictionary<string, OpenApiMediaType>();
                    var responseBodyGenerators = responseContent.Select(valuePair =>
                    {
                        var content = valuePair.Value;
                        var contentType = valuePair.Key.ToPascalCase();
                        var contentSchemaReference = openApiResponseVisitor.GetSchemaReference(content);
                        
                        var contentSpecification = new SourceGeneratorHelpers.GenerationSpecification(
                            ns: $"{responseContentNamespace}._{responseStatusCodePattern}",
                            typeName: Path.Combine(responseContentDirectory, responseStatusCodePattern,
                                contentType),
                            location: contentSchemaReference,
                            rebaseToRootPath: false);

                        var typeDeclaration = GenerateCode(context, contentSpecification, generationContext, globalOptions);
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
}