using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class MediaTypeExtensions
{
    internal static string GetMatchConditionExpression(this MediaTypeHeaderValue value, string mediaTypeVariableName)
    {
        var expressions = new List<string>();
        if (value.MediaType is not null)
        {
            expressions.Add(value.MediaType switch
            {
                "*/*" => "true",
                not null when value.MediaType.EndsWith("*") =>
                    $"""{mediaTypeVariableName}.{nameof(value.MediaType)}.{nameof(value.MediaType.StartsWith)}("{value.MediaType.TrimEnd('*')}", StringComparison.OrdinalIgnoreCase)""",
                _ =>
                    $"""{mediaTypeVariableName}.{nameof(value.MediaType)}.{nameof(value.MediaType.Equals)}("{value.MediaType}", StringComparison.OrdinalIgnoreCase)"""
            });
        }
        
        expressions.AddRange(value.Parameters.Select(parameter => 
            $"{mediaTypeVariableName}.{nameof(value.Parameters)}.{nameof(value.Parameters.Contains)}({(parameter.Value is null ?
                $"""new NameValueHeaderValue("{parameter.Name}")""" :
                $"""new NameValueHeaderValue("{parameter.Name}", "{parameter.Value}")""")})"));

        return string.Join(" && ", expressions);
    }

    internal static int GetPrecedence(this MediaTypeHeaderValue value) =>
         value.Parameters.Count + value.MediaType switch
        {
            "*/*" => 0,
            not null when value.MediaType.EndsWith("*") => 100,
            _ => 1000
        };
}