using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corvus.Json;

namespace Example.OpenApi32.Paths.FooFooId.Put;

internal partial class Operation
{
    public Operation()
    {
        HandleRequestValidationError = HandleValidationErrors;
    }

    private static Response.BadRequest400 HandleValidationErrors(Request request, ImmutableList<ValidationResult> validationResults)
    {
        switch (request.TryMatchAcceptMediaType<Response.BadRequest400>(out var matchedMediaType))
        {
            case false:
            case true when matchedMediaType == Response.BadRequest400.ApplicationJson.ContentMediaType:
                var response = validationResults.Select(result =>
                    Components.Responses.BadRequest.Content.ApplicationJson.RequiredErrorAndName.Create(
                        name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                        error: result.Message ?? string.Empty));
                return new Response.BadRequest400.ApplicationJson(
                    Components.Responses.BadRequest.Content.ApplicationJson.Create(response.ToArray()));
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
        }
    }

    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        _ = request.Query.Fee;
        _ = request.Path.FooId;
        
        switch (request.TryMatchAcceptMediaType<Response.OK200>(out var matchedMediaType))
        {
            case false:
            case true when matchedMediaType == Response.OK200.AnyApplication.ContentMediaType:
                return Task.FromResult<Response>(new Response.OK200.AnyApplication(
                    Components.Schemas.FooProperties.Create(name: request.Body.ApplicationJson?.Name),
                    "application/json") { Headers = new Response.OK200.ResponseHeaders { Status = 2 } });
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
        }
    }
}