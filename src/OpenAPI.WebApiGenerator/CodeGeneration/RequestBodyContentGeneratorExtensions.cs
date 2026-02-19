using System.Collections.Generic;
using System.Linq;
using OpenAPI.WebApiGenerator.Extensions;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal static class RequestBodyContentGeneratorExtensions
{
    internal static IEnumerable<RequestBodyContentGenerator> SortByContentType(
        this IEnumerable<RequestBodyContentGenerator> generators) =>
        generators
            .GroupBy(generator => generator.ContentType.Quality ?? 1)
            .OrderByDescending(grouping => grouping.Key)
            .SelectMany(grouping => grouping
                .OrderByDescending(generator => 
                    generator.ContentType.GetPrecedence()));
}