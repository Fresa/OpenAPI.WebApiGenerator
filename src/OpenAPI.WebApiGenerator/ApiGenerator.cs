using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Corvus.Json;
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
        jsonValidationExceptionGenerator.GenerateJsonValidationExceptionClass().AddTo(context);

        var endpointGenerator = new OperationGenerator(compilation, jsonValidationExceptionGenerator);
        var openApi = OpenApiDocument.Load(openApiDocumentFile.AsStream(), "json").Document ??
                      throw new InvalidOperationException(
                          $"Could not load OpenAPI document {openApiDocumentFile.Path}");
        
        var openApiUri = new JsonReference(openApi.BaseUri.ToString());
        var documentResolver = new PrepopulatedDocumentResolver();
        var openApiDocument = JsonDocument.Parse(generatorContext.OpenApiDocument.AsStream());
        if (!documentResolver.AddDocument(openApiUri, openApiDocument))
        {
            throw new InvalidOperationException("Could not add OpenApi document");
        }
        var generationContext = new SourceGeneratorHelpers.GenerationContext(documentResolver, globalOptions);
        var schemaGenerator = new SchemaGenerator(
            rootNamespace,
            context,
            generationContext);

        var openApiReference = new OpenApiReference<OpenApiDocument>(openApi, openApiDocument, openApiUri);
        var openApiVisitor = OpenApiVisitor.V2(openApiReference);

        var httpRequestExtensionsGenerator = new HttpRequestExtensionsGenerator(rootNamespace);
        httpRequestExtensionsGenerator.GenerateHttpRequestExtensionsClass().AddTo(context);
        
        var httpResponseExtensionsGenerator = new HttpResponseExtensionsGenerator(rootNamespace);
        httpResponseExtensionsGenerator.GenerateHttpResponseExtensionsClass().AddTo(context);

        var apiConfigurationGenerator = new ApiConfigurationGenerator(rootNamespace);
        apiConfigurationGenerator.GenerateClass().AddTo(context);

        var validationExtensionsGenerator = new ValidationExtensionsGenerator(rootNamespace);
        validationExtensionsGenerator.GenerateClass().AddTo(context);
        
        var operations = new List<(string Namespace, HttpMethod HttpMethod)>();
        foreach (var path in openApi.Paths)
        {
            var pathExpression = path.Key;
            var pathItem = path.Value;
            var openApiPathVisitor = openApiVisitor.Visit(pathItem);
            var pathParameterGenerators = new Dictionary<string, ParameterGenerator>();
            foreach (var parameter in pathItem.Parameters ?? [])
            {
                var schemaReference = openApiPathVisitor.GetSchemaReference(parameter);
                var typeDeclaration = schemaGenerator.Generate(schemaReference);
                pathParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] = new ParameterGenerator(typeDeclaration, parameter,
                    httpRequestExtensionsGenerator);
            }

            foreach (var openApiOperation in path.Value.GetOperations())
            {
                var openApiOperationVisitor = openApiPathVisitor.Visit(openApiOperation.Key);
                var operationMetadata = TypeMetadata.From(openApiOperationVisitor.Pointer);
                var operationDirectory = operationMetadata.Path;
                var operationNamespace = $"{rootNamespace}.{operationMetadata.Namespace}.{operationMetadata.Name}";
                var operationMethod = openApiOperation.Key;
                var operation = openApiOperation.Value;
                var operationParameterGenerators = new Dictionary<string, ParameterGenerator>(pathParameterGenerators);

                foreach (var parameter in operation.GetParameters())
                {
                    var schemaReference = openApiOperationVisitor.GetSchemaReference(parameter);
                    var typeDeclaration = schemaGenerator.Generate(schemaReference);
                    operationParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] = new ParameterGenerator(typeDeclaration, parameter,
                        httpRequestExtensionsGenerator);
                }

                var body = operation.RequestBody;
                var requestBodyGenerator = RequestBodyGenerator.Empty;
                if (body is not null)
                {
                    var contentGenerators = body.GetContent().Select(pair =>
                    {
                        var requestBodyContent = pair.Value;
                        var schemaReference = openApiOperationVisitor.GetSchemaReference(requestBodyContent);
                        var typeDeclaration = schemaGenerator.Generate(schemaReference);
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

                var responses = operation.Responses ??
                                throw new InvalidOperationException(
                                    $"No responses defined for operation at {openApiOperationVisitor.Pointer}");
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
                        var contentSchemaReference = openApiResponseVisitor.GetSchemaReference(content);
                        var typeDeclaration = schemaGenerator.Generate(contentSchemaReference);
                        return new ResponseBodyContentGenerator(valuePair.Key, typeDeclaration);
                    }).ToList();

                    var responseHeaderGenerators = response.Headers?.Select(valuePair =>
                    {
                        var name = valuePair.Key;
                        var header = valuePair.Value;
                        var responseHeaderSchema = openApiResponseVisitor.GetSchemaReference(header);
                        var typeDeclaration = schemaGenerator.Generate(responseHeaderSchema);
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
        operationRouterGenerator.ForMinimalApi(operations).AddTo(context);
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