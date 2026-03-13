namespace Example.OpenApi32.Paths.FooFooIdEvents.Post;

internal partial class Operation
{
    internal partial async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var content = request.Body.ApplicationJsonl;
        if (content == null)
        {
            throw new InvalidOperationException("missing content, this cannot occur");
        }
        while (await content.MoveNextAsync()
                   .ConfigureAwait(false))
        {
            var validationContext = content.ValidateCurrentItem();
            if (!validationContext.IsValid)
            {
                throw new InvalidOperationException("Invalid item");
            }
            _ = content.Current;
        }
            
        return new Response.Accepted202();
    }
}