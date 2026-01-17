using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Corvus.Json;
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
        
        var openApiProvider = openapiDocumentProvider
            .Combine(context.CompilationProvider)
            .Select((tuple, _) => (
                OpenApiDocument: tuple.Left,
                Compilation: tuple.Right
            ));

        context.RegisterSourceOutput(openApiProvider,
            WithExceptionReporting<(AdditionalText, Compilation)>(GenerateCode));
    }

    private static void GenerateCode(SourceProductionContext context, (
        AdditionalText OpenApiDocument, 
        Compilation Compilation) generatorContext)
    {
        var compilation = generatorContext.Compilation;
        var rootNamespace = compilation.Assembly.Name;
        
        var openApiDocumentFile = generatorContext.OpenApiDocument;
        var jsonValidationExceptionGenerator = new JsonValidationExceptionGenerator(rootNamespace);
        jsonValidationExceptionGenerator.GenerateJsonValidationExceptionClass().AddTo(context);

        var endpointGenerator = new OperationGenerator(compilation, jsonValidationExceptionGenerator);
        var openApiResult = OpenApiDocument.Load(openApiDocumentFile.AsStream(), "json");
        var openApiVersion = openApiResult.Diagnostic?.SpecificationVersion ??
                             throw new InvalidOperationException("Unknown openapi version");
        if (openApiResult.Diagnostic.Errors.Any())
        {
            throw new InvalidOperationException(
                openApiResult.Diagnostic.Errors.AggregateToString(
                    "Errors while parsing OpenAPI specification: ",
                    error => $"{(error.Pointer == null ? "" : $"{error.Pointer}: ")}{error.Message}"));
        }
        var openApi = openApiResult.Document ??
                      throw new InvalidOperationException(
                          $"Could not load OpenAPI document {openApiDocumentFile.Path}");

        
        var openApiUri = new JsonReference(openApi.BaseUri.ToString());
        var documentResolver = new PrepopulatedDocumentResolver();
        var openApiDocument = JsonDocument.Parse(generatorContext.OpenApiDocument.AsStream());
        if (!documentResolver.AddDocument(openApiUri, openApiDocument))
        {
            throw new InvalidOperationException("Could not add OpenApi document");
        }
        var schemaGenerator = SchemaGenerator.For(
            openApiVersion,
            documentResolver, 
            rootNamespace, 
            context);

        var openApiReference = new OpenApiReference<OpenApiDocument>(openApi, openApiDocument, openApiUri);
        var openApiVisitor = OpenApiVisitor.V(openApiVersion, openApiReference);

        var httpRequestExtensionsGenerator = new HttpRequestExtensionsGenerator(
            openApiVersion,
            rootNamespace);
        httpRequestExtensionsGenerator.GenerateHttpRequestExtensionsClass().AddTo(context);
        
        var httpResponseExtensionsGenerator = new HttpResponseExtensionsGenerator(rootNamespace,
            openApiVersion);
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
                pathParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] =
                    new ParameterGenerator(typeDeclaration,
                        parameter,
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
                    operationParameterGenerators[$"{parameter.GetName()}_{parameter.GetLocation()}"] =
                        new ParameterGenerator(typeDeclaration,
                            parameter,
                            httpRequestExtensionsGenerator);
                }

                var body = operation.RequestBody;
                var requestBodyGenerator = RequestBodyGenerator.Empty;
                if (body is not null)
                {
                    var contentGenerators = body.GetContent().Select(pair =>
                    {
                        var mediaType = pair.Value;
                        var schemaReference = openApiOperationVisitor.GetSchemaReference(mediaType);
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
                        // OpenAPI.NET is incorrectly adding content where there is none defined. 
                        // No content definition means NO content.
                        response.Content?.Where(content => 
                            openApiResponseVisitor.HasContent(content.Value)) ?? [];
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
                            openApiVersion);
                    }).ToList() ?? [];

                    return new ResponseContentGenerator(
                        responseStatusCodePattern,
                        responseBodyGenerators,
                        responseHeaderGenerators);
                }).ToList();
                
                var responseGenerator = new ResponseGenerator(
                    responseBodyGenerators, 
                    httpResponseExtensionsGenerator);
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