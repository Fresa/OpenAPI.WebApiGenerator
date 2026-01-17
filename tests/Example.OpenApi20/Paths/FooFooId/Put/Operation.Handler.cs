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

    private static Response.BadRequest400 HandleValidationErrors(ImmutableList<ValidationResult> validationResults)
    {
        var response = validationResults.Select(result =>
            Responses.BadRequest.RequiredErrorAndName.Create(
                name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                error: result.Message ?? string.Empty));
        return new Response.BadRequest400(
            Responses.BadRequest.Create(response.ToArray()));
    }

    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        _ = request.Query.Fee;
        _ = request.Path.FooId;
        _ = request.Header.Bar;

        var response = new Response.OK200(Definitions.FooProperties.Create(
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
    }
}