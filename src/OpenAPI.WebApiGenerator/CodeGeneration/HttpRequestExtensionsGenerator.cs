using System;
using System.IO;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class HttpRequestExtensionsGenerator(
    OpenApiSpecVersion openApiVersion,
    string @namespace)
{
    private const string HttpRequestExtensionsClassName = "HttpRequestExtensions";

    private readonly string _openApiVersion = openApiVersion switch
    {
        OpenApiSpecVersion.OpenApi2_0 => "2.0",
        OpenApiSpecVersion.OpenApi3_0 => "3.0",
        OpenApiSpecVersion.OpenApi3_1 => "3.1",
        _ => throw new NotSupportedException($"OpenAPI version {Enum.GetName(typeof(OpenApiSpecVersion), openApiVersion)} not supported")
    };
    
    internal string CreateBindParameterInvocation(
        string requestVariableName, 
        string bindingTypeName,
        IOpenApiParameter parameter)
    {
        using var textWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(textWriter, new OpenApiJsonWriterSettings()
        {
            InlineLocalReferences = true
        });
        Action<IOpenApiWriter> serialize = openApiVersion switch
        {
            OpenApiSpecVersion.OpenApi3_1 => parameter.SerializeAsV31,
            OpenApiSpecVersion.OpenApi3_0 => parameter.SerializeAsV3,
            OpenApiSpecVersion.OpenApi2_0 => parameter.SerializeAsV2,
            _ => throw new NotSupportedException(
                $"OpenAPI version {Enum.GetName(typeof(OpenApiSpecVersion), openApiVersion)} not supported")
        };
        serialize(jsonWriter);
        textWriter.Flush();
        
        return
            $""""
            {@namespace}.{HttpRequestExtensionsClassName}.Bind<{bindingTypeName}>(
            {requestVariableName},
            "{_openApiVersion}",
            """
            {textWriter.GetStringBuilder()}
            """)
            """";
    }
    
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
        using OpenAPI.ParameterStyleParsers;

        namespace {{{@namespace}}};

        internal static class {{{HttpRequestExtensionsClassName}}}
        {
            private static readonly ConcurrentDictionary<IParameter, IParameterValueParser> ParserCache = new();
            private static IParameterValueParser GetParser(IParameter parameter) => ParserCache.GetOrAdd(parameter, _ => parameter.CreateParameterValueParser());

            /// <summary>
            /// Binds an http parameter to a json type
            /// </summary>
            /// <param name="request"></param>
            /// <param name="openApiVersion">OpenAPI Version of the specification</param>
            /// <param name="parameterSpecificationAsJson">OpenAPI parameter specification formatted as json</param>
            /// <typeparam name="T">The type to bind</typeparam>
            /// <returns>The bound instance</returns>
            /// <exception cref="BadHttpRequestException"></exception>
            internal static T Bind<T>(this HttpRequest request, 
                string openApiVersion,
                string parameterSpecificationAsJson)
                where T : struct, IJsonValue<T>
            {
                var parameter = ParameterFactory.OpenApi(openApiVersion, parameterSpecificationAsJson);
                return parameter switch
                {
                    _ when parameter.InBody => T.Parse(request.BodyReader.AsStream()),
                    _ when TryParse<T>(request, parameter, out var value) => value.Value,
                    _ => T.Undefined
                };
            }

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
                    ? string.Join('&', values.Select(value => $"{parameter.Name}=${value}"))
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