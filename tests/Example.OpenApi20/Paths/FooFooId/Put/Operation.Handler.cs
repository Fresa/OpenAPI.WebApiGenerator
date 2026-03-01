using System.Collections.Immutable;
using Corvus.Json;

namespace Example.OpenApi20.Paths.FooFooId.Put;

internal partial class Operation
{
    public Operation()
    {
        HandleRequestValidationError = HandleValidationErrors;
        ValidateResponse = false;
        ValidationLevel = ValidationLevel.Detailed;
    }

    private static Response.BadRequest400 HandleValidationErrors(Request request, ImmutableList<ValidationResult> validationResults)
    {
        switch (request.TryMatchAcceptMediaType<Response.BadRequest400>(out var matchedMediaType))
        {
            case false:
            case true when matchedMediaType == Response.BadRequest400.ApplicationJson.ContentMediaType:
                var response = validationResults.Select(result =>
                    Responses.BadRequest.RequiredErrorAndName.Create(
                        name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                        error: result.Message ?? string.Empty));
                return new Response.BadRequest400.ApplicationJson(
                    Responses.BadRequest.Create(response.ToArray()));
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
            case true when matchedMediaType == Response.OK200.ApplicationJson.ContentMediaType:
                var response = new Response.OK200.ApplicationJson(Definitions.FooProperties.Create(
                        name: request.Body.ApplicationJson?.Name))
                {
                    Headers = new Response.OK200.ResponseHeaders
                    {
                        Status = 2
                    }
                };
                var validationContext = response.Validate(ValidationLevel);
                return !validationContext.IsValid
                    ? throw new JsonValidationException("Response is not valid", validationContext.Results)
                    : Task.FromResult<Response>(response);
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
        }
    }
}