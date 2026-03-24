using Example.OpenApi32.Components.Schemas;

namespace Example.OpenApi32.Paths.FooFooIdEvents.Post;

internal partial class Operation
{
    internal partial async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var content = 
            request.Body.ApplicationJsonSeq ?? 
            request.Body.ApplicationGeoJsonSeq ??
            request.Body.ApplicationJsonl ??
            request.Body.ApplicationXNdjson ??
            request.Body.ApplicationXJsonlines as SequentialJsonEnumerable<FooProperties> ?? 
            throw new InvalidOperationException("missing content, this cannot occur");
        
        var importedEvents = 0;
        await foreach (var (item, validationContext) in content
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            if (!validationContext.IsValid)
            {
                return CreateRequestValidationErrorResponse(request, validationContext);
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