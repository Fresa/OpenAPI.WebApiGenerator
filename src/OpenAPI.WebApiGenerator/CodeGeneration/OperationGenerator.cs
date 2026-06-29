using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Corvus.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class OperationGenerator(Compilation compilation,
    JsonValidationExceptionGenerator jsonValidationExceptionGenerator,
    AuthGenerator authGenerator,
    Options options)
{
    private readonly List<(string Namespace, string Path)> _missingHandlers = [];

    internal SourceCode Generate(string @namespace,
        string path,
        string pathTemplate,
        (HttpMethod Method, OpenApiOperation Operation) operation,
        ParameterGenerator[] parameters)
    {
        var endpointSource =
$$"""
#nullable enable
using Corvus.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace {{@namespace}};
{{ new[]
{
    operation.Operation.Summary.AsComment("summary"),
    operation.Operation.Description.AsComment("remarks", "para")
}.RemoveEmptyLines().AggregateToString()}}
internal partial class Operation
{
    internal const string PathTemplate = "{{pathTemplate}}";
    internal const string Method = "{{operation.Method}}";

    private const string RequestItemKey = "OpenAPI.WebApiGenerator.Request";
    
    /// <summary>
    /// Set validation level for requests and responses
    /// </summary>
    internal ValidationLevel ValidationLevel { get; init; } = ValidationLevel.{{Enum.GetName(typeof(ValidationLevel), options.ValidationLevel)}};

    /// <summary>
    /// Should responses be validated?
    /// If the response has already been validated, this can be disabled to avoid redundant validation.
    /// </summary>
    internal bool ValidateResponse { get; init; } = true;

{{HandleMethodSignature.Indent(4)}};

    /// <summary>
    /// Set a custom delegate to handle request validation errors.
    /// <exception cref="JsonValidationException"></exception>
    /// </summary>
    private Func<Request, ImmutableList<ValidationResult>, Response> HandleRequestValidationError { get; } = (_, validationResult) => 
        {{jsonValidationExceptionGenerator.CreateThrowJsonValidationExceptionInvocation("Request is not valid", "validationResult")}};

    /// <summary>
    /// Create a request validation error response.
    /// <exception cref="JsonValidationException"></exception>
    /// <param name="request">The invalid request</param>
    /// <param name="ValidationContext">The validation context describing the validation errors</param>
    /// </summary>
    private Response CreateRequestValidationErrorResponse(Request request, ValidationContext validationContext)
    {
        var configuration = request.HttpContext.RequestServices.GetRequiredService<WebApiConfiguration>();
        return HandleRequestValidationError(request, validationContext.Results.WithLocation(configuration.OpenApiSpecificationUri));
    }
    
{{authGenerator.GenerateAuthFilters(operation.Operation, parameters, out var requiresAuth).Indent(4)}}
{{(requiresAuth ? 
"""

    /// <summary>
    /// Set a custom delegate to handle unauthorized responses.
    /// </summary>
    private Func<Request, Response> HandleUnauthorized { get; } = _ => new Response.Unauthorized();
    
    /// <summary>
    /// Set a custom delegate to handle forbidden responses.
    /// </summary>
    private Func<Request, Response> HandleForbidden { get; } = _ => new Response.Forbidden();
    
""" : "")}}
    /// <summary>
    /// Binds the request object
    /// </summary>
    internal sealed class BindRequestFilter : IEndpointFilter
    {
        /// <inheritdoc/>
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var cancellationToken = httpContext.RequestAborted;
            
            var request = await Request.BindAsync(httpContext, cancellationToken)
                .ConfigureAwait(false);

            httpContext.Items.Add(RequestItemKey, request);
            
            return await next(context)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handle a operation.
    /// <exception cref="JsonValidationException"></exception>
    /// </summary>
    /// <param name="context">The current http context</param>
    /// <param name="operation">The operation</param>
    /// <param name="configuration">Web api configuration</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <returns>An awaitable task</returns>
    internal static async Task HandleAsync(
        HttpContext context, 
        [FromServices] Operation operation,
        [FromServices] WebApiConfiguration configuration, 
        CancellationToken cancellationToken)
    {
        var request = (Request) context.Items[RequestItemKey]!;
        
        var validationContext = request.Validate(operation.ValidationLevel);
        if (!validationContext.IsValid)
        {
            operation.HandleRequestValidationError(request, validationContext.Results.WithLocation(configuration.OpenApiSpecificationUri))
                .WriteTo(context.Response);
            return;
        }
        
        var response = await operation.HandleAsync(request, cancellationToken)
            .ConfigureAwait(false);
        operation.Validate(response, configuration)
            .WriteTo(context.Response);
    }
    
    /// <summary>
    /// Validates a response
    /// <exception cref="{{JsonValidationExceptionGenerator.ClassName}}">
    /// Thrown when validation fails.
    /// </exception>
    /// </summary>
    /// <param name="response">The response to validate</param>
    /// <param name="configuration">Web api configuration</param>
    /// <returns>The validated response</returns>
    internal Response Validate(Response response, WebApiConfiguration configuration)
    {
        if (!ValidateResponse)
            return response;
        
        var validationContext = response.Validate(ValidationLevel);
        if (validationContext.IsValid)
            return response;
        
        var validationResult = validationContext.Results.WithLocation(configuration.OpenApiSpecificationUri);
        {{jsonValidationExceptionGenerator.CreateThrowJsonValidationExceptionInvocation("Response is not valid", "validationResult")}};
    }
}{{(requiresAuth ? 
"""

/// <inheritdoc/>
internal abstract partial class Response
{
    /// <summary>
    /// Unauthorized response
    /// </summary>
    internal sealed class Unauthorized : Response
    {
        /// <inheritdoc/>
        internal override void WriteTo(HttpResponse httpResponse)
        {
            httpResponse.StatusCode = 401;
        }
        
        /// <inheritdoc/>
        internal override ValidationContext Validate(ValidationLevel validationLevel) => ValidationContext.ValidContext; 
    }

    /// <summary>
    /// Forbidden response
    /// </summary>
    internal sealed class Forbidden : Response
    {
        /// <inheritdoc/>
        internal override void WriteTo(HttpResponse httpResponse)
        {
            httpResponse.StatusCode = 403;
        }

        /// <inheritdoc/>
        internal override ValidationContext Validate(ValidationLevel validationLevel) => ValidationContext.ValidContext; 
    }
}
""" : "")}}
#nullable restore
""";
        
        var hasImplementedHandleMethod = compilation.GetSymbolsWithName("Operation", SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Where(symbol => symbol.ContainingNamespace.ToDisplayString() == @namespace)
            .Any(HasImplementedHandleMethod);
        if (!hasImplementedHandleMethod)
        {
            _missingHandlers.Add((@namespace, path));
        }
        
        return new SourceCode(
            $"{path}/Operation.g.cs",
            endpointSource);
    }

    private const string HandleMethodSignature =
        """
        /// <summary>
        /// Handles a request for this operation.
        /// </summary>
        internal partial Task<Response> HandleAsync(Request request, CancellationToken cancellationToken)
        """;

    private static bool HasImplementedHandleMethod(INamedTypeSymbol typeSymbol)
    {
        var members = typeSymbol.GetMembers("HandleAsync");
        return members.OfType<IMethodSymbol>()
            .Any(method =>
                HasImplementation(method) &&
                method.Parameters is [{ Type.Name: "Request" }, { Type.Name: "CancellationToken" }]);
    }

    private static bool HasImplementation(IMethodSymbol method) =>
        !method.IsPartialDefinition || method.PartialImplementationPart != null;

    internal bool TryGenerateMissingHandlers(
        out (SourceCode SourceCode, Diagnostic Diagnostic)[] missingHandlers)
    {
        if (_missingHandlers.Count == 0)
        {
            missingHandlers = [];
            return false;
        }

        missingHandlers =
            _missingHandlers.Select(handler =>
                {
                    var filename = $"{handler.Path}/Operation.Handler.g.cs";
                    return (new SourceCode(
                            filename,
                            GenerateMissingHandler(handler.Namespace)),
                        CreateMissingHandlersDiagnosticMessage(handler.Namespace, filename));
                })
                .ToArray();
        return true;
    }

    private static Diagnostic CreateMissingHandlersDiagnosticMessage(string @namespace, string filePath) =>
        Diagnostic.Create(
            Af1001MissingApiOperationHandler,
            Location.Create(filePath, new TextSpan(), new LinePositionSpan()),
            messageArgs: [@namespace, filePath]);

    private static string GenerateMissingHandler(string @namespace) =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace {{@namespace}}
            {
                internal partial class Operation
                {
                    {{HandleMethodSignature}}
                    {
                        throw new NotImplementedException();
                    }
                }
            }
          """;

    private static readonly DiagnosticDescriptor Af1001MissingApiOperationHandler =
        new(
            id: "AF1001",
            title: "Missing API operation handlers",
            messageFormat: $"HandleAsync is missing for the {{0}} operation. A generated stub can be copied from {{1}}.",
            category: "Api",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
}