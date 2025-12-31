using System.Collections.Immutable;
using Corvus.Json;
using Example.Api.FooFooId.UpdateFoo.Responses._200;

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
            Responses._400.ApplicationJson.RequiredErrorAndName.Create(
                name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                error: result.Message ?? string.Empty));
        return new Response.BadRequest400(
            Responses._400.ApplicationJson.Create(response.ToArray()));
    }

    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        _ = request.Fee;
        _ = request.FooId;
        _ = request.Bar;

        var response = new Response.OK200(ApplicationJson.Create(
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