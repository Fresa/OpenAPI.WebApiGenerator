using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class EnumerableExtensions
{
    internal static string AggregateToString<T>(this IEnumerable<T> items, Func<T, string> convert) =>
        items.AggregateToString(new StringBuilder().AppendLine(), convert);
    internal static string AggregateToString<T>(this IEnumerable<T> items, string firstLine, Func<T, string> convert) =>
        items.AggregateToString(new StringBuilder(firstLine), convert);
    private static string AggregateToString<T>(this IEnumerable<T> items, StringBuilder stringBuilder, Func<T, string> convert) =>
        items
            .Aggregate(stringBuilder, (builder, item) =>
                builder.AppendLine(convert(item)))
            .ToString()
            .TrimEnd();

    internal static IEnumerable<(T item, int i)> WithIndex<T>(this IEnumerable<T> items) =>
        items.Select((arg1, i) => (arg1, i));
}