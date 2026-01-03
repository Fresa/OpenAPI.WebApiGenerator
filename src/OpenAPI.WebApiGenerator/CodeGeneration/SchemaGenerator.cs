using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.SourceGeneratorTools;
using Microsoft.CodeAnalysis;
using OpenAPI.WebApiGenerator.Extensions;
using JsonPointer = OpenAPI.WebApiGenerator.OpenApi.Visitor.JsonPointer;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class SchemaGenerator(string rootNamespace,
    SourceProductionContext context,
    SourceGeneratorHelpers.GenerationContext generationContext)
{
    private static readonly IDocumentResolver MetaSchemaResolver = SourceGeneratorHelpers.CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = SourceGeneratorHelpers.CreateVocabularyRegistry(MetaSchemaResolver);
    private readonly Dictionary<string, TypeDeclaration> _typeCache = new();
    
    internal TypeDeclaration Generate(JsonReference reference)
    {
        if (_typeCache.TryGetValue(reference, out var typeDeclaration))
        {
            return typeDeclaration;
        }
        var pointer = JsonPointer.ParseFrom(reference);
        var segments = pointer.Segments.Select(segment =>
                segment.ToPascalCase())
            .ToArray();
        var path = Path.Combine(segments);

        var namespaceSegments =
            segments.Select(segment => 
                int.TryParse(segment[..1], out _) ? $"_{segment}" : segment);
        var @namespace = string.Join(".", namespaceSegments.Prepend(rootNamespace));
        var generationSpecification = new SourceGeneratorHelpers.GenerationSpecification(
            ns: @namespace,
            typeName: path,
            location: reference,
            rebaseToRootPath: false);

        typeDeclaration = GenerateCode(context, generationSpecification, generationContext);
        _typeCache.Add(reference, typeDeclaration);
        return typeDeclaration;
    }
    
    private static TypeDeclaration GenerateCode(SourceProductionContext context,
        SourceGeneratorHelpers.GenerationSpecification specification,
        SourceGeneratorHelpers.GenerationContext generationContext)
    {
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

        foreach (var spec in typesToGenerate.GenerationSpecifications)
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
            
            if (Path.HasExtension(spec.TypeName))
            {
                var typeName = Path.GetFileName(spec.TypeName)!;
                namedTypes.Add(
                    new CSharpLanguageProvider.NamedType(
                        rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                        typeName,
                        spec.Namespace,
                        spec.Accessibility));
            }
            else
            {
                filePath = spec.TypeName!;
            }
            
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

        foreach (var codeFile in generatedCode)
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
    
    private static readonly DiagnosticDescriptor Crv1001ErrorGeneratingCSharpCode =
        new(
            id: "CRV1001",
            title: "JSON Schema Type Generator Error",
            messageFormat: "Error generating C# code: {0}: {1}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

}