using System;
using System.Collections.Generic;
using System.Linq;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V3;

internal sealed partial class OpenApiV3Visitor
{
    private sealed class ParametersVisitor : 
        OpenApiVisitor<IList<IOpenApiParameter>>
    {
        private ParametersVisitor(OpenApiReference<IList<IOpenApiParameter>> openApiReference) : base(openApiReference)
        {
            VisitParameters();
        }

        internal Dictionary<IOpenApiParameter, JsonReference> Schemas { get; } = new();
        
        internal static ParametersVisitor Visit(OpenApiReference<IList<IOpenApiParameter>> openApiReference) => 
            new(openApiReference);

        private void VisitParameters()
        {
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

                var parameter = OpenApiDocument.Single(apiParameter =>
                    apiParameter.GetName() == parameterName &&
                    apiParameter.GetLocation() == parameterLocation);
                
                if (!TryVisit([parameterIndex.ToString(), "schema"], out var schemaPointer))
                {
                    schemaPointer = Visit(
                        "content",
                        parameter.Content?.Single().Key ??
                        throw new InvalidOperationException("Parameter doesn't contain a schema"),
                        "schema");
                }

                Schemas.Add(parameter, 
                    new JsonReference(Reference.Uri, schemaPointer.ToString().AsSpan()));
                
                parameterIndex++;
            }
        }
    }
}