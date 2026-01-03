using System.Collections.Immutable;
using Corvus.Json;

namespace Example.Api.FooFooId.UpdateFoo;

internal partial class Operation
{
    public Operation()
    {
        HandleValidationError = HandleValidationErrors;
    }

    private static Response HandleValidationErrors(ImmutableList<ValidationResult> validationResults)
    {
        var response = validationResults.Select(result =>
            Responses.BadRequest.Schema.Schema.RequiredErrorAndName.Create(
                name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                error: result.Message ?? string.Empty));
        return new Response.BadRequest400(
            Responses.BadRequest.Schema.Schema.Create(response.ToArray()));
    }

    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        _ = request.Query.Fee;
        _ = request.Path.FooId;
        _ = request.Header.Bar;

        var response = new Response.OK200(Example.Api.Definitions.FooProperties.FooProperties.Create(
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