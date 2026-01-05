using System.Collections.Immutable;
using Corvus.Json;

namespace Example.Api.Paths.FooFooId.Put;

internal partial class Operation
{
    public Operation()
    {
        HandleValidationError = HandleValidationErrors;
    }

    private static Response HandleValidationErrors(ImmutableList<ValidationResult> validationResults)
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
        return Task.FromResult<Response>(response);
    }
}