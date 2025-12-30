using System;
using System.Collections.Generic;
using Corvus.Json;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor.V2;

internal sealed partial class OpenApiV2Visitor
{
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