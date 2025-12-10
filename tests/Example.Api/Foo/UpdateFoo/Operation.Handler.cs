using Example.Api.Foo.UpdateFoo.Responses._200;

namespace Example.Api.Foo.UpdateFoo;

internal partial class Operation
{
    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
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