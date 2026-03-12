using System.Net.Http.Headers;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class SequentialJsonEnumeratorGenerator(string @namespace)
{
    internal string GenerateConstructorInstance(
        MediaTypeHeaderValue mediaType, 
        TypeDeclaration itemTypeDeclaration,
        string streamParameterReference,
        string schemaLocation,
        string validationLevel) =>
$"""
new {GetFullyQualifiedTypeName(mediaType)}<{itemTypeDeclaration.FullyQualifiedDotnetTypeName()}>({streamParameterReference}, "{schemaLocation}", {validationLevel})
""";

    internal string GetFullyQualifiedTypeName(MediaTypeHeaderValue mediaType) =>
        $"{@namespace}.{mediaType.MediaType.ToLower() switch
        {
            "application/jsonl" or "application/x-ndjson" or "application/x-jsonlines" => "ApplicationJsonlEnumerator",
            "application/json-seq" or "application/geo+json-seq" => "ApplicationJsonSeqEnumerator",
            _ => mediaType.MediaType.ToPascalCase()
        }}";
    internal SourceCode GenerateClasses() => new("SequentialJsonEnumerators.g.cs",
$$"""
#nullable enable
using Microsoft.AspNetCore.Authorization;
using System;

namespace {{@namespace}};

/// <summary>
/// Base class for sequential json enumerators
/// </summary>
internal abstract class SequentialJsonEnumerator<T>(
    Stream stream,
    string schemaLocation, 
    ValidationLevel validationLevel) : IAsyncEnumerator<T> 
    where T : struct, IJsonValue<T>
{
    private PipeReader PipeReader { get; } = PipeReader.Create(stream);
    protected abstract byte Delimiter { get; } 
    public ValueTask DisposeAsync() => PipeReader.CompleteAsync();

    private int _itemPosition;
    
    /// <inheritdoc/>
    public async ValueTask<bool> MoveNextAsync()
    {
        do
        {
            var result = await PipeReader.ReadAsync()
                .ConfigureAwait(false);
            var buffer = result.Buffer;
            var position = buffer.PositionOf(Delimiter);

            if (position != null)
            {
                var data = buffer.Slice(0, position.Value);
                Current = ParseItem(data);
                PipeReader.AdvanceTo(position.Value);
                return true;
            }

            if (result.IsCompleted)
            {
                PipeReader.AdvanceTo(buffer.End);
                return false;
            }

            PipeReader.AdvanceTo(buffer.Start, buffer.End);
            _itemPosition++;
        } while (true);
    }
    
    /// <inheritdoc/>
    public T Current { get; private set; }
    
    /// <summary>
    /// Parse the read item
    /// </summary>
    /// <param name="data">Data read up until the Delimiter</param>
    /// <returns>The parsed item</returns>
    protected abstract T ParseItem(ReadOnlySequence<byte> data); 
    
    /// <summary>
    /// Validates the current item
    /// </summary>
    /// <returns>The validation result</returns>
    internal ValidationContext ValidateCurrentItem() => 
        Current.Validate($"{schemaLocation}/{_itemPosition}", true, ValidationContext.ValidContext, validationLevel);
}

/// <summary>
/// Sequential json enumerator for jsonl
/// </summary>
internal sealed class ApplicationJsonlEnumerator<T>(Stream stream, string schemaLocation, ValidationLevel validationLevel) : 
    SequentialJsonEnumerator<T>(stream, schemaLocation, validationLevel) 
    where T : struct, IJsonValue<T>
{
    protected override byte Delimiter => 0x0A;
    protected override T ParseItem(ReadOnlySequence<byte> data) => T.Parse(data);
}

/// <summary>
/// Sequential json enumerator for json-seq
/// </summary>
internal sealed class ApplicationJsonSeqEnumerator<T>(Stream stream, string schemaLocation, ValidationLevel validationLevel) : 
    SequentialJsonEnumerator<T>(stream, schemaLocation, validationLevel) 
    where T : struct, IJsonValue<T>
{
    private const byte RecordSeparator = 0x1E;
    protected override byte Delimiter => 0x0A;

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