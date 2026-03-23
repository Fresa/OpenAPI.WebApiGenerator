namespace Example.OpenApi32.Paths.FooFooIdEvents.Get;

internal partial class Operation
{
    internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        switch (request.TryMatchAcceptMediaType<Response.OK200>(out ContentMediaType<Response.OK200>? matchedMediaType))
        {
            case false:
            case true when matchedMediaType == Response.OK200.ApplicationJsonl.ContentMediaType:
                var response = new Response.OK200.ApplicationJsonl(request);
                response.WriteItem(Components.Schemas.FooProperties.Create(name: "foo1"));
                response.WriteItem(Components.Schemas.FooProperties.Create(name: "foo2"));
                return Task.FromResult<Response>(response);
            default:
                throw new NotImplementedException($"Content media type {matchedMediaType} has not been implemented");
        }
    }
}