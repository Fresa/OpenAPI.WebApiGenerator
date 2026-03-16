using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class SequentialMediaTypesGenerator(string @namespace)
{
    internal string GenerateConstructorInstance(
        MediaTypeHeaderValue mediaType, 
        TypeDeclaration itemTypeDeclaration,
        string streamParameterReference) =>
$"""
new {GetFullyQualifiedTypeName(mediaType, itemTypeDeclaration)}({streamParameterReference})
""";

    internal string GetFullyQualifiedTypeName(
        MediaTypeHeaderValue mediaType,
        TypeDeclaration itemTypeDeclaration) =>
        $"{@namespace}.{mediaType.MediaType.ToLower() switch
        {
            "application/jsonl" or "application/x-ndjson" or "application/x-jsonlines" => "ApplicationJsonlEnumerable",
            "application/json-seq" or "application/geo+json-seq" => "ApplicationJsonSeqEnumerable",
            _ => mediaType.MediaType.ToPascalCase()
        }}<{itemTypeDeclaration.FullyQualifiedDotnetTypeName()}>";
    
    internal SourceCode GenerateClasses() => new("SequentialMediaTypes.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Buffers;
using System.IO.Pipelines;

namespace {{@namespace}};

/// <summary>
/// Base class for sequential json enumerable
/// </summary>
internal abstract class SequentialJsonEnumerable<T>(Stream stream) : IAsyncEnumerable<(T, ValidationContext)> 
    where T : struct, IJsonValue<T>
{
    private int _itemPosition = -1;
    private ValidationLevel _validationLevel = default;
    private string _schemaLocation = "#";
    private T? _current;

    /// <summary>
    /// Delimiter between each item
    /// </summary>
    protected abstract byte Delimiter { get; } 
    
    /// <summary>
    /// Does the sequence require ending with a delimiter? 
    /// </summary>
    protected abstract bool RequiresDelimiterAfterLastItem { get; }
    
    /// <inheritdoc/>
    public async IAsyncEnumerator<(T, ValidationContext)> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var pipeReader = PipeReader.Create(stream);
        try
        {
            do
            {
                var result = await pipeReader.ReadAsync(cancellationToken)
                    .ConfigureAwait(false);
                var buffer = result.Buffer;
                var position = buffer.PositionOf(Delimiter);

                switch (result.IsCompleted)
                {
                    // Found an item
                    case false or true when position is not null:
                        var data = buffer.Slice(0, position.Value);
                        _itemPosition++;
                        _current = ParseItem(data);
                        pipeReader.AdvanceTo(buffer.GetPosition(1, position.Value));
                        yield return (_current.Value, ValidateCurrentItem());
                        break;
                    // No more data
                    case true when buffer.IsEmpty:
                        yield break;
                    // No more data to read, data was found, but no delimiter.
                    // End delimiter is optional, so parse any found data.
                    case true when !RequiresDelimiterAfterLastItem:
                        _itemPosition++;
                        _current = ParseItem(buffer);
                        pipeReader.AdvanceTo(buffer.End);
                        yield return (_current.Value, ValidateCurrentItem());
                        yield break;
                    // No more data to read, data was found, but no delimiter.
                    // End delimiter is required, so discard any found data
                    case true:
                        pipeReader.AdvanceTo(buffer.End);
                        yield break;
                    // More data exist, and no item found yet
                    default:
                        pipeReader.AdvanceTo(buffer.Start, buffer.End);
                        break;
                }
            } while (true);
        }
        finally
        {
            await pipeReader.CompleteAsync()
                .ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Parse the read item
    /// </summary>
    /// <param name="data">Data read up until the Delimiter</param>
    /// <returns>The parsed item</returns>
    protected abstract T ParseItem(ReadOnlySequence<byte> data); 
    
    private ValidationContext ValidateCurrentItem() => 
        _current?.Validate($"{_schemaLocation}/{_itemPosition}", true, ValidationContext.ValidContext, _validationLevel) ?? ValidationContext.ValidContext;
        
    /// <summary>
    /// Validates the sequence
    /// </summary>
    /// <param name="schemaLocation">The location of the schema describing the sequence</param>
    /// <param name="isRequired">Is the sequence required?</param>
    /// <param name="validationContext">Current validation context</param>
    /// <param name="validationLevel">The validation level</param>
    /// <returns>The validation result</returns>
    internal ValidationContext Validate(string schemaLocation, bool isRequired, ValidationContext validationContext, ValidationLevel validationLevel)
    {
        _schemaLocation = schemaLocation;
        _validationLevel = validationLevel;
        return validationContext;
    }
}

/// <summary>
/// Sequential json enumerable for jsonl
/// </summary>
internal sealed class ApplicationJsonlEnumerable<T>(Stream stream) : 
    SequentialJsonEnumerable<T>(stream) 
    where T : struct, IJsonValue<T>
{
    protected override byte Delimiter => 0x0A;
    protected override bool RequiresDelimiterAfterLastItem => false;
    protected override T ParseItem(ReadOnlySequence<byte> data) => T.Parse(data);
}

/// <summary>
/// Sequential json enumerable for json-seq
/// </summary>
internal sealed class ApplicationJsonSeqEnumerable<T>(Stream stream) : 
    SequentialJsonEnumerable<T>(stream) 
    where T : struct, IJsonValue<T>
{
    private const byte RecordSeparator = 0x1E;
    protected override byte Delimiter => 0x0A;
    protected override bool RequiresDelimiterAfterLastItem => true;

    protected override T ParseItem(ReadOnlySequence<byte> data)
    {
        var rsPosition = data.PositionOf(RecordSeparator);
        
        // RS should be first.
        // If it is not, then the data is incomplete and invalid,
        // let JSON validation handle it
        if (rsPosition.HasValue && rsPosition.Value.GetInteger() == 0)
        {
            data = data.Slice(data.GetPosition(1));
        }

        return T.Parse(data);
    }
}

#nullable restore
""");
}