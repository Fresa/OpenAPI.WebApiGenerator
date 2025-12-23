using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class RequestGenerator(
    List<ParameterGenerator> parameterGenerators, 
    RequestBodyGenerator bodyGenerator)
{
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

                    {{parameterGenerators.Aggregate(new StringBuilder(), (builder, generator) =>
                        builder.AppendLine(generator.GenerateRequestProperty()))}}

                    {{bodyGenerator.GenerateRequestProperty("Body")}}
                    
                    public static {{(isAsync ? "async" : "")}} Task<Request> BindAsync(HttpContext context, CancellationToken cancellationToken)
                    {
                        var httpRequest = context.Request;
                        var request = new Request
                        {
                            HttpContext = context,
                            {{parameterGenerators.Aggregate(new StringBuilder(), (builder, generator) =>
                                builder.AppendLine(generator.GenerateRequestBindingDirective("httpRequest")))}}
                                
                            {{bodyBindingDirective}}
                        };

                        return {{(isAsync ? "request" : "Task.FromResult(request)")}};
                    }
                    
                    internal ValidationContext Validate(ValidationLevel validationLevel)
                    {
                        var validationContext = ValidationContext.ValidContext;
                        {{bodyGenerator.GenerateValidateDirective("Body", "validationContext", "validationLevel")}}
                        {{parameterGenerators.Aggregate(new StringBuilder(), (builder, generator) =>
                            builder.AppendLine(
                                $"validationContext = Validate({generator.AsRequired(generator.PropertyName)}, {generator.IsParameterRequired.ToString().ToLowerInvariant()});"))}}
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
                }
                #nullable restore
              """);
    }
}