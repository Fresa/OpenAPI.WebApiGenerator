using Example.Api.FooFooId.UpdateFoo.Responses._200;

namespace Example.Api.FooFooId.UpdateFoo;

internal partial class Operation
{
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