using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Corvus.Json;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.OpenApi.JsonPointer;

internal sealed class OpenApiV2Visitor :
    OpenApiVisitor<OpenApiDocument>, IOpenApiVisitor
{
    private OpenApiV2Visitor(OpenApiReference<OpenApiDocument> openApiReference) : base(openApiReference)
    {
        VisitPathItems();
    }

    private readonly Dictionary<IOpenApiPathItem, JsonReference> _pathItems = new ();
    
    internal static OpenApiV2Visitor Visit(OpenApiReference<OpenApiDocument> openApiReference) => 
        new(openApiReference);

    private void VisitPathItems()
    {
        foreach (var path in OpenApiDocument.Paths)
        {
            var pointer = Visit("paths", path.Key);
            _pathItems.Add(path.Value, new JsonReference(Reference.Uri, pointer.ToString().AsSpan()));
        }
    }
    
    public IOpenApiPathItemVisitor Visit(IOpenApiPathItem pathItem) => 
        PathItemVisitor.Visit(new OpenApiReference<IOpenApiPathItem>(pathItem, Document, _pathItems[pathItem]));
    
    private sealed class PathItemVisitor : 
        OpenApiVisitor<IOpenApiPathItem>, IOpenApiPathItemVisitor
    {
        private Dictionary<IOpenApiParameter, JsonReference> _parameterSchemaReferences = new();
        private readonly Dictionary<HttpMethod, OperationVisitor> _operations = new();

        private PathItemVisitor(OpenApiReference<IOpenApiPathItem> openApiReference) : base(openApiReference)
        {
            VisitParameters();
            VisitOperations();
        }

        private void VisitParameters()
        {
            if (OpenApiDocument.Parameters == null)
            {
                return;
            }

            var parametersPointer = Visit("parameters");
            var parametersVisitor = ParametersVisitor.Visit(
                new OpenApiReference<IList<IOpenApiParameter>>(
                    OpenApiDocument.Parameters,
                    Document,
                    new JsonReference(Reference.Uri, parametersPointer.ToString().AsSpan())));
            _parameterSchemaReferences = parametersVisitor.Schemas;
        }

        private void VisitOperations()
        {
            foreach (var openApiOperation in OpenApiDocument.Operations ?? [])
            {
                var method = openApiOperation.Key;
                var operation = openApiOperation.Value; 
                var operationPointer = Visit(method.Method.ToLowerInvariant());
                var operationReference = new JsonReference(Reference.Uri, operationPointer.ToString().AsSpan());
                _operations.Add(method,
                    OperationVisitor.Visit(
                        new OpenApiReference<OpenApiOperation>(operation, Document, operationReference)));
            }
        }
        
        internal static PathItemVisitor Visit(OpenApiReference<IOpenApiPathItem> openApiReference) => 
            new(openApiReference);

        public JsonReference GetSchemaReference(IOpenApiParameter parameter) => 
            _parameterSchemaReferences[parameter];

        public IOpenApiOperationVisitor Visit(HttpMethod httpMethod) =>
            _operations[httpMethod];

        private sealed class OperationVisitor :
            OpenApiVisitor<OpenApiOperation>, IOpenApiOperationVisitor
        {
            private Dictionary<IOpenApiParameter, JsonReference> _parameterSchemaReferences = new();
            private JsonReference? _bodySchemaReference;
            private Dictionary<IOpenApiResponse, IOpenApiResponseVisitor> _responseVisitors = new();
            
            private OperationVisitor(OpenApiReference<OpenApiOperation> openApiReference) : base(openApiReference)
            {
                VisitParameters();
                VisitResponses();
            }
            
            private void VisitParameters()
            {
                if (OpenApiDocument.Parameters == null)
                {
                    return;
                }
                var parametersPointer = Visit("parameters");
                var parametersVisitor = ParametersVisitor.Visit(
                    new OpenApiReference<IList<IOpenApiParameter>>(
                        OpenApiDocument.Parameters,
                        Document,
                        new JsonReference(Reference.Uri, parametersPointer.ToString().AsSpan())));
                _parameterSchemaReferences = parametersVisitor.Schemas;
                _bodySchemaReference = parametersVisitor.BodySchema;
            }

            private void VisitResponses()
            {
                foreach (var response in OpenApiDocument.Responses ?? [])
                {
                    var responsePointer = Visit("responses", response.Key);
                    var responseReference = new JsonReference(Reference.Uri, responsePointer.ToString().AsSpan());
                    var responseVisitor =
                        ResponseVisitor.Visit(
                            new OpenApiReference<IOpenApiResponse>(response.Value, Document, responseReference));
                    _responseVisitors.Add(response.Value, responseVisitor);
                }
            }
            
            internal static OperationVisitor Visit(
                OpenApiReference<OpenApiOperation> openApiReference) =>
                new(openApiReference);

            public JsonReference GetSchemaReference(IOpenApiParameter parameter) =>
                _parameterSchemaReferences[parameter];

            public JsonReference GetSchemaReference(OpenApiMediaType requestBodyContent) => 
                _bodySchemaReference ?? throw new InvalidOperationException("Operation doesn't define a body");

            public IOpenApiResponseVisitor Visit(IOpenApiResponse response) => 
                _responseVisitors[response];
        }
    }

    private sealed class ResponseVisitor :
        OpenApiVisitor<IOpenApiResponse>, IOpenApiResponseVisitor
    {
        private ResponseVisitor(OpenApiReference<IOpenApiResponse> openApiReference) : base(openApiReference)
        {
            VisitContent();
        }

        private JsonReference? _schemaReference;

        internal static ResponseVisitor Visit(OpenApiReference<IOpenApiResponse> openApiReference) =>
            new(openApiReference);

        private void VisitContent()
        {
            if (TryVisit(["schema"], out var schemaPointer))
            {
                _schemaReference = new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan());
            }
        }

        public JsonReference GetSchemaReference(OpenApiMediaType mediaType) => 
            _schemaReference ?? throw new InvalidOperationException("Response has no content defined");

        public bool HasContent() => _schemaReference != null;
    }

    private sealed class ParametersVisitor : 
        OpenApiVisitor<IList<IOpenApiParameter>>
    {
        private ParametersVisitor(OpenApiReference<IList<IOpenApiParameter>> openApiReference) : base(openApiReference)
        {
            VisitParameters();
        }

        internal Dictionary<IOpenApiParameter, JsonReference> Schemas { get; } = new();
        internal JsonReference? BodySchema { get; private set; }
        
        internal static ParametersVisitor Visit(OpenApiReference<IList<IOpenApiParameter>> openApiReference) => 
            new(openApiReference);

        private void VisitParameters()
        {
            Dictionary<(string Name, string Location), JsonReference> parameters = new();
            var parameterIndex = 0;
            while (TryVisit([parameterIndex.ToString()], out var parameterPointer))
            {
                var parameterNameElement = JsonPointerUtilities.ResolvePointer(
                    Document,
                    parameterPointer.Append("name").ToString().AsSpan());
                var parameterName = parameterNameElement.GetString() ??
                                    throw new InvalidOperationException("parameter doesn't have a name");
                var parameterLocationElement = JsonPointerUtilities.ResolvePointer(
                    Document,
                    parameterPointer.Append("in").ToString().AsSpan());
                var parameterLocation = parameterLocationElement.GetString() ??
                                        throw new InvalidOperationException("parameter doesn't have a location");

                if (!TryVisit([parameterIndex.ToString(), "schema"], out var schemaPointer))
                {
                    schemaPointer = parameterPointer;
                }

                parameters.Add((parameterName, parameterLocation),
                    new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan()));
                if (parameterLocation == "body")
                {
                    BodySchema = parameters[(parameterName, parameterLocation)];
                }
                
                parameterIndex++;
            }

            foreach (var parameter in OpenApiDocument)
            {
                Schemas.Add(parameter, parameters[(parameter.GetName(), parameter.GetLocation())]);
            }
        }
    }
}
