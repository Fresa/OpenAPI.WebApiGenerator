using System.Collections.Immutable;
using Corvus.Json;

namespace Example.OpenApi31.Paths.FooFooId.Put;

internal partial class Operation
{
    public Operation()
    {
        HandleRequestValidationError = HandleValidationErrors;
    }

    private static Response.BadRequest400 HandleValidationErrors(ImmutableList<ValidationResult> validationResults)
    {
        var response = validationResults.Select(result =>
            Components.Responses.BadRequest.Content.ApplicationJson.RequiredErrorAndName.Create(
                name: result.Location?.SchemaLocation.ToString() ?? string.Empty,
                error: result.Message ?? string.Empty));
        return new Response.BadRequest400.ApplicationJson(
            Components.Responses.BadRequest.Content.ApplicationJson.Create(response.ToArray()));
    }

    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        _ = request.Query.Fee;
        _ = request.Path.FooId;
        _ = request.Header.Bar;

        var response = new Response.OK200.ApplicationJson(Components.Schemas.FooProperties.Create(
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

internal abstract class Test
{
    protected abstract string A { get; }

    
}

internal class Testar : Test
{
    public Testar(string a)
    {
        A = a;
    }

    protected sealed override string A { get; }
}