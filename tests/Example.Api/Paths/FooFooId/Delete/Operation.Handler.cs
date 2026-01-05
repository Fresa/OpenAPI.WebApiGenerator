namespace Example.Api.Paths.FooFooId.Delete;

internal partial class Operation
{
    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var response = new Response.OK200();
        return Task.FromResult<Response>(response);
    }
}