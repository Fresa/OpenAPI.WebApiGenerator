using System;
using Microsoft.CodeAnalysis;

namespace OpenAPI.WebApiGenerator.Tests.Utils;

internal static class DiagnosticExtensions
{
    internal static string GetFormattedMessage(this Diagnostic diagnostic) => 
        diagnostic.GetMessage().Replace(" |", Environment.NewLine);
}