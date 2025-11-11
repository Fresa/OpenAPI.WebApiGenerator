using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class EnumerableExtensions
{
    internal static string AggregateToString<T>(this IEnumerable<T> items, Func<T, string> convert) =>
        items
            .Aggregate(new StringBuilder(), (builder, item) => 
                builder.AppendLine(convert(item)))
            .ToString();
}