using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class RequestGenerator(
    List<ParameterGenerator> parameterGenerators,
    RequestBodyGenerator bodyGenerator)
{
    private readonly IEnumerable<IGrouping<string, ParameterGenerator>> _parameterGeneratorsGroupedByLocation =
        parameterGenerators
            .GroupBy(generator => generator.Location.ToPascalCase())
            .Where(group => group.Any());
    
    internal SourceCode GenerateRequestClass(string @namespace, string path)
    {
        var bodyBindingDirective = bodyGenerator.GenerateRequestBindingDirective("Body",
            "httpRequest",
            out var isAsync);
        if (bodyBindingDirective != string.Empty)
        {
            bodyBindingDirective = new StringBuilder()
                .AppendLine(",")
                .Append(bodyBindingDirective)
                .ToString();
        }
        
        return new SourceCode($"{path}/Request.g.cs",
$$"""
#nullable enable
using Corvus.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Net.Http.Headers;

namespace {{@namespace}};

/// <summary>
/// Contains the operation's request object
/// </summary>
internal partial class Request
{
    internal required HttpContext HttpContext { get; init; }{{_parameterGeneratorsGroupedByLocation.AggregateToString(group => 
$$"""
    /// <summary>
    /// {{group.Key}} parameters
    /// </summary>
    internal required {{group.Key}}Parameters {{group.Key}} { get; init; }  
""")}}
{{bodyGenerator.GenerateRequestProperty("Body").Indent(4)}}
    /// <summary>
    /// Bind request object from http request
    /// </summary>
    /// <param name="context">Http context to bind from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An awaitable task for the request object</returns>
    public static {{(isAsync ? "async " : "")}}Task<Request> BindAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var httpRequest = context.Request;
        var request = new Request
        {
            HttpContext = context,{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
$$"""
            {{group.Key}} = new {{group.Key}}Parameters
            {{{group.AggregateToString(generator =>
                    generator.GenerateRequestBindingDirective("httpRequest"))
                .TrimEnd(',').Indent(16)}}
            },
""").TrimEnd(',')}}{{bodyBindingDirective.Indent(12).TrimStart()}}
        };

        return {{(isAsync ? "request" : "Task.FromResult(request)")}};
    }
    
    /// <summary>
    /// Returns the best match if an acceptable media type is found.
    /// </summary>
    /// <param name="mediaTypes">Media types to match against</param>
    /// <param name="matchedMediaType">Matched media type if method returns true</param>
    /// <returns>True if a matched media type was found</returns>
    internal bool TryMatchAcceptMediaType<T>(
        [NotNullWhen(true)] out MediaTypeHeaderValue? matchedMediaType) where T : class, IResponse =>
        HttpContext.Request.TryMatchAcceptMediaType(T.ContentMediaTypes, out matchedMediaType);
    
    /// <summary>
    /// Validate the request
    /// </summary>
    /// <param name="validationLevel">Validation level</param>
    /// <returns>The validation result</returns>
    internal ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = ValidationContext.ValidContext.UsingStack().UsingResults();{{
            bodyGenerator.GenerateValidateDirective("Body", "validationContext", "validationLevel").Indent(8)
        }}{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
            group.AggregateToString(generator =>
        $"""
         validationContext = ({group.Key}.{generator.AsRequired(generator.PropertyName)}).Validate("{generator.SchemaLocation}", {generator.IsParameterRequired.ToString().ToLowerInvariant()}, validationContext, validationLevel);
         """).Trim()).Indent(8)}}
        return validationContext;
    }{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
$$"""
    /// <summary>
    /// {{group.Key}} parameters
    /// </summary>
    internal sealed class {{group.Key}}Parameters
    {{{group.AggregateToString(generator => 
        generator.GenerateRequestProperty()).Indent(8)}}
    }
""")}}
}
#nullable restore
""");
    }
}