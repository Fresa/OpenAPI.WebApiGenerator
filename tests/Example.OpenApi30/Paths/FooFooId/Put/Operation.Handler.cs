using System.Collections.Immutable;
using Corvus.Json;

namespace Example.OpenApi30.Paths.FooFooId.Put;

internal partial class Operation
{
    public Operation()
    {
        HandleRequestValidationError = HandleValidationErrors;
        ValidateResponse = true;
    }

    private static Response.BadRequest400 HandleValidationErrors(Request request, ImmutableList<ValidationResult> validationResults)
    {
        switch (request.TryMatchAcceptMediaType<Response.BadRequest400>(out var matchedMediaType))
        {
            case false:
            case true when ReferenceEquals(matchedMediaType, Response.BadRequest400.ApplicationJson.ContentMediaType):
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
        _ = request.Header.Bar;

        switch (request.TryMatchAcceptMediaType<Response.OK200>(out var matchedMediaType))
        {
            case false:
            case true when ReferenceEquals(matchedMediaType, Response.OK200.ApplicationJson.ContentMediaType):
                return Task.FromResult<Response>(new Response.OK200.ApplicationJson(
                    Components.Schemas.FooProperties.Create(name: request.Body.ApplicationJson?.Name))
                {
                    Headers = new Response.OK200.ResponseHeaders { Status = 2 }
                });
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
        }
    }
}
