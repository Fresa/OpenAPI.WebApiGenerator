using System;
using System.Threading;
using System.Threading.Tasks;
using Example.OpenApi32.Components.Schemas;

namespace Example.OpenApi32.Paths.FooFooIdEvents.Get;

internal partial class Operation
{
    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        switch (request.TryMatchAcceptMediaType<Response.OK200>(out ContentMediaType<Response.OK200>? matchedMediaType))
        {
            case false:
            case true when matchedMediaType == Response.OK200.ApplicationJsonl.ContentMediaType:
                var jsonl = new Response.OK200.ApplicationJsonl(request);
                WriteItems(jsonl.WriteItem);
                return Task.FromResult<Response>(jsonl);
            case true when matchedMediaType == Response.OK200.ApplicationXJsonlines.ContentMediaType:
                var jsonLines = new Response.OK200.ApplicationXJsonlines(request);
                WriteItems(jsonLines.WriteItem);
                return Task.FromResult<Response>(jsonLines);
            case true when matchedMediaType == Response.OK200.ApplicationXNdjson.ContentMediaType:
                var ndJson = new Response.OK200.ApplicationXNdjson(request);
                WriteItems(ndJson.WriteItem);
                return Task.FromResult<Response>(ndJson);
            case true when matchedMediaType == Response.OK200.ApplicationJsonSeq.ContentMediaType:
                var jsonSeq = new Response.OK200.ApplicationJsonSeq(request);
                WriteItems(jsonSeq.WriteItem);
                return Task.FromResult<Response>(jsonSeq);
            case true when matchedMediaType == Response.OK200.ApplicationGeoJsonSeq.ContentMediaType:
                var geoJsonSeq = new Response.OK200.ApplicationGeoJsonSeq(request);
                WriteItems(geoJsonSeq.WriteItem);
                return Task.FromResult<Response>(geoJsonSeq);
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");

        }
        
        void WriteItems(Action<FooProperties> write)
        {
            write(FooProperties.Create(name: "foo1"));
            write(FooProperties.Create(name: "foo2"));                    
        }
    }
}