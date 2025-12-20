using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace OpenAPI.WebApiGenerator.OpenApi;

internal class OpenApiPointerVisitorV3(JsonNode document) : OpenApiPointerVisitor(document)
{
    public override IDisposable Visit(OpenApiPaths paths) => Record("paths");

    public override IDisposable Visit(KeyValuePair<string, IOpenApiPathItem> path) => Record(path.Key);

    public override IDisposable Visit(KeyValuePair<HttpMethod, OpenApiOperation> operation) =>
        Record(operation.Key.ToString().ToLowerInvariant());

    public override IDisposable Visit(IList<IOpenApiParameter>? parameters) => 
        parameters == null ? EmptyRecord : Record("parameters");

    public override IDisposable Visit(IOpenApiParameter parameter, int index) => Record(index.ToString());
    public override IDisposable VisitSchema(IOpenApiParameter parameter)
    {
        return EmptyRecord;
    }

    public override IDisposable Visit(IOpenApiRequestBody requestBody) => Record("requestBody");

    public override IDisposable VisitContent() => Record("content");

    public override IDisposable Visit(OpenApiMediaType mediaType, string contentType) => Record(contentType);

    public override IDisposable VisitResponses() => Record("responses");

    public override IDisposable Visit(IOpenApiResponse response, string statusCode) => Record(statusCode);

    public override IDisposable VisitHeaders() => Record("headers");

    public override IDisposable Visit(IOpenApiHeader header, string headerName) => Record(headerName);
}

internal abstract class OpenApiPointerVisitor(JsonNode document)
{
    private readonly JsonPointer _refPointer = new("$ref");
    internal string GetPointer() => _visitedPointers.Peek();

    private readonly Stack<string> _visitedPointers = new(["/"]);

    public static OpenApiPointerVisitor V3(JsonNode openApiSpec) => new OpenApiPointerVisitorV3(openApiSpec);
    
    public abstract IDisposable Visit(OpenApiPaths paths);

    public abstract IDisposable Visit(KeyValuePair<string, IOpenApiPathItem> path);

    public abstract IDisposable Visit(KeyValuePair<HttpMethod, OpenApiOperation> operation);

    public abstract IDisposable Visit(IList<IOpenApiParameter>? parameters);

    public abstract IDisposable Visit(IOpenApiParameter parameter, int index);
    public abstract IDisposable VisitSchema(IOpenApiParameter parameter);
    public abstract IDisposable Visit(IOpenApiRequestBody requestBody);

    public abstract IDisposable VisitContent();

    public abstract IDisposable Visit(OpenApiMediaType mediaType, string contentType);

    public abstract IDisposable VisitResponses();

    public abstract IDisposable Visit(IOpenApiResponse response, string statusCode);

    public abstract IDisposable VisitHeaders();

    public abstract IDisposable Visit(IOpenApiHeader header, string headerName);

    protected IDisposable Record(string segment)
    {
        var encoded = segment.Replace("~", "~0").Replace("/", "~1");
        var jsonPointer = $"{_visitedPointers.Peek().TrimEnd('/')}/{encoded}";
        var pointer = new JsonPointer(jsonPointer);
        var node = pointer.Find(document);
        if (node == null)
        {
            throw new InvalidOperationException($"{pointer} doesn't exist in openapi document");
        }

        jsonPointer = _refPointer.Find(node) switch
        {
            JsonValue value => value.GetValue<string>()
                .TrimStart('#'),
            _ => jsonPointer
        };
        _visitedPointers.Push(jsonPointer);
        return new Visitor(Rewind);
            
        void Rewind()
        {
            _visitedPointers.Pop();
        }
    }

    protected readonly IDisposable EmptyRecord = new Visitor(() => { });
    private class Visitor(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}

public class OpenApiJsonPointerVisitor : OpenApiVisitorBase
{
    public override void Visit(IOpenApiSchema schema)
    {
#pragma warning disable RS1035
        // Console.WriteLine(PathString);
#pragma warning restore RS1035
    }
}
