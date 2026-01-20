using System;
using System.Text.Json.Nodes;
using Corvus.Json;
using Microsoft.CodeAnalysis;

namespace OpenAPI.WebApiGenerator;

internal sealed class Options
{
    internal static readonly Options Default = new();
    internal static Options From(AdditionalText text)
    {
        var content = text.GetText();
        if (content == null)
        {
            return Default;
        }

        var json = JsonNode.Parse(content.ToString());
        if (json == null)
        {
            return Default;
        }

        var options = new Options();
        var validationLevelNode = json[ValidationLevelKey];
        if (validationLevelNode != null)
        {
            var value = validationLevelNode.GetValue<string>();
            if (!Enum.TryParse<ValidationLevel>(value, out var validationLevel))
            {
                throw new ArgumentOutOfRangeException($"Could not parse option {ValidationLevelKey}: {value}");
            }
            options.ValidationLevel = validationLevel;
        }

        return options;
    }

    private const string ValidationLevelKey = nameof(ValidationLevel);
    
    internal ValidationLevel ValidationLevel { get; private set; } = ValidationLevel.Detailed;
}