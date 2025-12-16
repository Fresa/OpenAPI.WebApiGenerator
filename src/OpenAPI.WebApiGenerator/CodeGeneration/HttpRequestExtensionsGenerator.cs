namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpRequestExtensionsGenerator(string @namespace)
{
    private const string HttpRequestExtensionsClassName = "HttpRequestExtensions";
    
    internal string CreateBindParameterInvocation(
        string requestVariableName, 
        string bindingTypeName,
        string parameterSpecificationAsJson)
    {
        return
            $""""
            {@namespace}.{HttpRequestExtensionsClassName}.Bind<{bindingTypeName}>(
            {requestVariableName},
            """
            {parameterSpecificationAsJson}
            """
            )
            """";
    }
    
    internal string CreateBindBodyInvocation(
        string requestVariableName, 
        string bindingTypeName)
    {
        return
            $""""
             await {@namespace}.{HttpRequestExtensionsClassName}.BindBodyAsync<{bindingTypeName}>(
                {requestVariableName}, cancellationToken)
                    .ConfigureAwait(false)
             """";
    }
    
    internal SourceCode GenerateHttpRequestExtensionsClass() =>
        new($"{HttpRequestExtensionsClassName}.g.cs",
        $$$""""
        #nullable enable
        using System.Collections.Concurrent;
        using System.Text.Json;
        using Corvus.Json;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Routing;
        using Microsoft.Extensions.Primitives;
        using OpenAPI.ParameterStyleParsers.OpenApi20;
        using OpenAPI.ParameterStyleParsers.OpenApi20.ParameterParsers;

        namespace {{{@namespace}}};

        internal static class {{{HttpRequestExtensionsClassName}}}
        {
            private static readonly ConcurrentDictionary<Parameter, ParameterValueParser> ParserCache = new();

            /// <summary>
            /// Binds an http parameter to a json type
            /// </summary>
            /// <param name="request"></param>
            /// <param name="parameterSpecificationAsJson"></param>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            /// <exception cref="BadHttpRequestException"></exception>
            internal static T Bind<T>(this HttpRequest request, 
                string parameterSpecificationAsJson)
                where T : struct, IJsonValue<T>
            {
                var parameter = Parameter.FromOpenApi20ParameterSpecification(parameterSpecificationAsJson);
                var value = parameter switch
                {
                    null => T.Undefined,
                    _ when parameter.InBody => T.Parse(request.BodyReader.AsStream()),
                    _ when TryGetValue(request, parameter, out var stringValue) =>
                        Parse<T>(parameter, stringValue),
                    _ => T.Undefined
                };

                return Validate(value);
            }

            internal static async Task<T> BindBodyAsync<T>(this HttpRequest request, CancellationToken cancellationToken)
                where T : struct, IJsonValue<T>
            {
                var document = await JsonDocument.ParseAsync(request.Body, 
                    cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                var value = T.FromJson(document.RootElement.Clone());

                return Validate(value);
            }

            private static T Validate<T>(T value) where T : struct, IJsonValue<T>
            {
                var validationContext = value.Validate(ValidationContext.ValidContext.UsingResults(), ValidationLevel.Verbose);
                if (validationContext.IsValid)
                {
                    return value;
                }

                var validationResults = validationContext.Results.IsEmpty
                    ? "None"
                    : JsonSerializer.Serialize(validationContext.Results, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                throw new BadHttpRequestException($$"""
                                                    Object of type {{typeof(T)}} could not be parsed'.
                                                    "Validation results: {{validationResults}}
                                                    """);
            }

            private static T Parse<T>(Parameter parameter, string? stringValue)
                where T : struct, IJsonValue<T>
            {
                var parser = ParserCache.GetOrAdd(parameter, ParameterValueParser.Create);
                if (!parser.TryParse(stringValue, out var instance, out var error))
                {
                    throw new BadHttpRequestException(error);
                }

                return instance == null ? T.Null : T.Parse(instance.ToJsonString());
            } 

            private static bool TryGetValue(this HttpRequest request, Parameter parameter, out string? stringValue) =>
                parameter switch
                {
                    _ when parameter.InHeader => TryGetHeaderValue(request.Headers, parameter, out stringValue),
                    _ when parameter.InFormData => TryGetFormDataValue(request.Form, parameter, out stringValue),
                    _ when parameter.InPath => TryGetPathValue(request.RouteValues, parameter, out stringValue),
                    _ when parameter.InQuery => TryGetQueryValue(request.Query, parameter, out stringValue),
                    _ => throw new InvalidOperationException($"Parameter {parameter.Name} has an unknown location")
                };

            private static bool TryGetQueryValue(IQueryCollection query, Parameter parameter, out string? stringValue)
            {
                stringValue = null;
                return query.TryGetValue(parameter.Name, out var values) &&
                       TryGetValue(values, parameter, out stringValue);
            }

            private static bool TryGetPathValue(RouteValueDictionary requestPath, Parameter parameter, out string? stringValue)
            {
                if (!requestPath.TryGetValue(parameter.Name, out var value))
                {
                    stringValue = null;
                    return false;
                }

                stringValue = value switch
                {
                    null => null,
                    string strValue => strValue,
                    _ => throw new InvalidOperationException(
                        $"Route value of '{value}' with type '{value.GetType()}' is not supported")
                };
                return true;
            }

            private static bool TryGetFormDataValue(IFormCollection requestForm, Parameter parameter, out string? stringValue)
            {
                stringValue = null;
                return requestForm.TryGetValue(parameter.Name, out var values) && TryGetValue(values, parameter, out stringValue);
            }

            private static bool TryGetHeaderValue(IHeaderDictionary headers, Parameter parameter, out string? stringValue)
            {
                stringValue = null;
                return headers.TryGetValue(parameter.Name, out var values) &&
                       TryGetValue(values, parameter, out stringValue);
            }

            private static bool TryGetValue(StringValues values, Parameter parameter, out string? stringValue)
            {
                if (values.Count == 0)
                {
                    stringValue = null;
                    return false;
                }
                stringValue = parameter.ValueIncludesKey
                    ? string.Join('&', values.Select(value => $"{parameter.Name}=${value}"))
                    : values.Single();
                return true;
            }
        }
        #nullable restore
        """");
}