using System.Text.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class OutputSchemaValidator(ILogger<OutputSchemaValidator> logger)
{
    public void Validate(object? output, JsonElement? schema, string definitionName)
    {
        if (schema is not { } schemaElement)
        {
            return;
        }

        JsonElement outputElement;
        try
        {
            var outputJson = JsonSerializer.Serialize(output);
            outputElement = JsonDocument.Parse(outputJson).RootElement;
        }
        catch (JsonException)
        {
            logger.SchemaViolation(definitionName, "$", "serializable", "non-serializable");
            return;
        }

        var violations = new List<SchemaViolation>();
        ValidateElement(outputElement, schemaElement, "$", violations);

        foreach (var violation in violations)
        {
            logger.SchemaViolation(definitionName, violation.Path, violation.Expected, violation.Actual);
        }
    }

    private static void ValidateElement(JsonElement value, JsonElement schema, string path, List<SchemaViolation> violations)
    {
        if (schema.TryGetProperty("type", out var typeProp))
        {
            var expectedType = typeProp.GetString();
            var actualType = GetJsonTypeName(value.ValueKind);

            if (expectedType is not null && !string.Equals(expectedType, actualType, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new SchemaViolation(path, expectedType, actualType));
                return;
            }
        }

        // Check required properties
        if (value.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var requiredProp)
            && requiredProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var reqElement in requiredProp.EnumerateArray())
            {
                var reqName = reqElement.GetString();
                if (reqName is not null && !value.TryGetProperty(reqName, out _))
                {
                    violations.Add(new SchemaViolation($"{path}.{reqName}", "present", "missing"));
                }
            }
        }

        // Check properties
        if (value.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var propertiesProp)
            && propertiesProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var schemaProp in propertiesProp.EnumerateObject())
            {
                if (value.TryGetProperty(schemaProp.Name, out var valueProp))
                {
                    ValidateElement(valueProp, schemaProp.Value, $"{path}.{schemaProp.Name}", violations);
                }
            }
        }

        // Check array items
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var itemsSchema))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                ValidateElement(item, itemsSchema, $"{path}[{index}]", violations);
                index++;
            }
        }
    }

    private static string GetJsonTypeName(JsonValueKind kind) =>
        kind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            JsonValueKind.Null => "null",
            _ => "undefined"
        };
}
