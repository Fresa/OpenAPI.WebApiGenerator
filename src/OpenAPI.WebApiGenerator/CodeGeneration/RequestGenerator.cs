using System.Collections.Generic;
using System.Linq;
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
        return new SourceCode($"{path}/Request.g.cs",
$$"""
#nullable enable
using Corvus.Json;

namespace {{@namespace}};

internal partial class Request
{
    internal required HttpContext HttpContext { get; init; }
{{_parameterGeneratorsGroupedByLocation.AggregateToString(group => 
$$"""
    internal required {{group.Key}}Parameters {{group.Key}} { get; init; }  
""")}}
{{bodyGenerator.GenerateRequestProperty("Body")}}
    
    public static {{(isAsync ? "async" : "")}} Task<Request> BindAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var httpRequest = context.Request;
        var request = new Request
        {
            HttpContext = context,
{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
$$"""
            {{group.Key}} = new {{group.Key}}Parameters
            {
                {{group
                    .AggregateToString(generator =>
                        generator.GenerateRequestBindingDirective("httpRequest"))
                    .TrimEnd(',').Indent(16)}}
            },
""").TrimEnd(bodyBindingDirective == string.Empty ? [','] : [])
}}
                
            {{bodyBindingDirective.Indent(12)}}
        };

        return {{(isAsync ? "request" : "Task.FromResult(request)")}};
    }
    
    internal ValidationContext Validate(ValidationLevel validationLevel)
    {
        var validationContext = ValidationContext.ValidContext;
        {{bodyGenerator.GenerateValidateDirective("Body", "validationContext", "validationLevel").Indent(8)}}
{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
            group.AggregateToString(generator =>
$"""
        validationContext = Validate({group.Key}.{generator.AsRequired(generator.PropertyName)}, {generator.IsParameterRequired.ToString().ToLowerInvariant()});
"""))
}}
        return validationContext;
        
        ValidationContext Validate<T>(T value, 
            bool isRequired) 
            where T : struct, IJsonValue<T>
        {
            if (!isRequired && value.IsUndefined())
            {
                return validationContext;
            }
          
            return value.Validate(validationContext, validationLevel);
        }
    }
    
{{_parameterGeneratorsGroupedByLocation.AggregateToString(group =>
$$"""
    internal sealed class {{group.Key}}Parameters
    {
        {{group.AggregateToString(generator => generator.GenerateRequestProperty()).Indent(8)}}
    }
""")}}
}
#nullable restore
""");
    }
}