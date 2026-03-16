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

        var importedEvents = 0;
        await foreach (var (item, validationContext) in content
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!validationContext.IsValid)
            {
                throw new InvalidOperationException("Invalid item");
            }
            importedEvents++;
        }
            
        return new Response.Accepted202
        {
            Headers = new Response.Accepted202.ResponseHeaders
            {
                ImportedEvents = importedEvents 
            }
        };
    }
}