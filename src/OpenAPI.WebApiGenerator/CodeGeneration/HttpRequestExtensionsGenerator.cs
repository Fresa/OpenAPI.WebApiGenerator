using Microsoft.OpenApi;
using OpenAPI.WebApiGenerator.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpRequestExtensionsGenerator(
    OpenApiSpecVersion openApiVersion,
    string @namespace)
{
    private const string HttpRequestExtensionsClassName = "HttpRequestExtensions";

    internal string CreateBindParameterInvocation(
        string requestVariableName, 
        string bindingTypeName,
        IOpenApiParameter parameter) =>
        $""""
         {@namespace}.{HttpRequestExtensionsClassName}.Bind<{bindingTypeName}>(
         {requestVariableName},
         """
         {parameter.Serialize(openApiVersion)}
         """)
         """";

    internal string CreateBindBodyInvocation(
        string requestVariableName, 
        string bindingTypeName)
    {
        return
$"""
await {@namespace}.{HttpRequestExtensionsClassName}.BindBodyAsync<{bindingTypeName}>(
    {requestVariableName}, cancellationToken)
.ConfigureAwait(false)
""";
    }
    
    internal SourceCode GenerateHttpRequestExtensionsClass() =>
        new($"{HttpRequestExtensionsClassName}.g.cs",
        $$$""""
        #nullable enable
        using System.Collections.Concurrent;
        using System.Diagnostics.CodeAnalysis;
        using System.Text.Json;
        using Corvus.Json;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.Primitives;
        using Microsoft.Net.Http.Headers;
        using OpenAPI.ParameterStyleParsers;

        namespace {{{@namespace}}};

        /// <summary>
        /// Extension methods for http request objects
        /// </summary>
        internal static class {{{HttpRequestExtensionsClassName}}}
        {
            private const string ParameterValueParserVersion = "{{{openApiVersion.GetParameterVersion()}}}";
            
            private static readonly ConcurrentDictionary<IParameter, IParameterValueParser> ParserCache = new();
            private static IParameterValueParser GetParser(IParameter parameter) => 
                ParserCache.GetOrAdd(parameter, _ => 
                    parameter.CreateParameterValueParser());
            
            private static readonly ConcurrentDictionary<string, IParameter> ParameterCache = new();
            private static IParameter GetParameter(string parameterSpecificationAsJson) => 
                ParameterCache.GetOrAdd(parameterSpecificationAsJson, _ => 
                    ParameterFactory.OpenApi(ParameterValueParserVersion, parameterSpecificationAsJson));

            /// <summary>
            /// Binds an http parameter to a json type
            /// </summary>
            /// <param name="request">Request to bind from</param>
            /// <param name="parameterSpecificationAsJson">OpenAPI parameter specification formatted as json</param>
            /// <typeparam name="T">The type to bind</typeparam>
            /// <returns>The bound instance</returns>
            internal static T Bind<T>(this HttpRequest request, 
                string parameterSpecificationAsJson)
                where T : struct, IJsonValue<T>
            {
                var parameter = GetParameter(parameterSpecificationAsJson);
                return parameter switch
                {
                    _ when parameter.InBody => T.Parse(request.BodyReader.AsStream()),
                    _ when TryParse<T>(request, parameter, out var value) => value.Value,
                    _ => T.Undefined
                };
            }

            /// <summary>
            /// Binds an http body to a json type
            /// </summary>
            /// <param name="request">Request to bind from</param>
            /// <param name="cancellationToken">Cancellation token</param>
            /// <typeparam name="T">The type to bind</typeparam>
            /// <returns>An awaitable task to the bound instance</returns>
            internal static async Task<T> BindBodyAsync<T>(this HttpRequest request,
                    CancellationToken cancellationToken)
                where T : struct, IJsonValue<T>
            {
                var document = await JsonDocument.ParseAsync(request.Body, 
                    cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                return T.FromJson(document.RootElement.Clone());
            }
           
            private static bool TryParse<T>(this HttpRequest request, IParameter parameter, [NotNullWhen(true)] out T? value) 
                where T : struct, IJsonValue<T> =>
                parameter switch
                {
                    _ when parameter.InHeader => TryParseHeader<T>(request.Headers, parameter, out value),
                    _ when parameter.InFormData => TryParseForm<T>(request.Form, parameter, out value),
                    _ when parameter.InPath => TryParsePath<T>(request.RouteValues, parameter, out value),
                    _ when parameter.InQuery => TryParseQuery<T>(request.Query, parameter, out value),
                    _ => throw new InvalidOperationException($"Parameter {parameter.Name} has an unknown location")
                };

            private static bool TryParseQuery<T>(IQueryCollection query, IParameter parameter, [NotNullWhen(true)] out T? value)
                where T : struct, IJsonValue<T>
            {
                value = null;
                return query.TryGetValue(parameter.Name, out var values) &&
                       TryParse<T>(values, parameter, out value);
            }

            private static bool TryParsePath<T>(RouteValueDictionary requestPath, IParameter parameter, [NotNullWhen(true)] out T? value)
                where T : struct, IJsonValue<T>
            {
                if (!requestPath.TryGetValue(parameter.Name, out var objValue))
                {
                    value = default;
                    return false;
                }

                var stringValue = objValue switch
                {
                    null => null,
                    string strValue => strValue,
                    _ => throw new InvalidOperationException(
                        $"Route value of '{objValue}' with type '{objValue.GetType()}' is not supported")
                };
                
                var parser = GetParser(parameter);
                value = Parse<T>(parser, stringValue);
                return true;
            }

            private static bool TryParseForm<T>(IFormCollection requestForm, IParameter parameter, [NotNullWhen(true)] out T? value)
                where T : struct, IJsonValue<T>
            {
                value = default;
                return requestForm.TryGetValue(parameter.Name, out var values) && TryParse<T>(values, parameter, out value);
            }

            private static bool TryParseHeader<T>(IHeaderDictionary headers, IParameter parameter, [NotNullWhen(true)] out T? value)
                where T : struct, IJsonValue<T>
            {
                value = default;
                return headers.TryGetValue(parameter.Name, out var values) &&
                       TryParse<T>(values, parameter, out value);
            }

            private static bool TryParse<T>(StringValues values, IParameter parameter, [NotNullWhen(true)] out T? value)
                where T : struct, IJsonValue<T>
            {
                if (values.Count == 0)
                {
                    value = default;
                    return false;
                }
                
                var parser = GetParser(parameter);
                var stringValue = parser.ValueIncludesParameterName
                    ? string.Join('&', values.Select(value => $"{parameter.Name}={value}"))
                    : values.Single();
                
                value = Parse<T>(parser, stringValue);
                return true;
            }
            
            private static T Parse<T>(IParameterValueParser parser, string? value)
                where T : struct, IJsonValue<T>
            {
                if (!parser.TryParse(value, out var instance, out var error))
                {
                    throw new BadHttpRequestException(error);
                }
            
                return instance == null ? T.Null : T.Parse(instance.ToJsonString());
            }
        }
        #nullable restore
        """");
}