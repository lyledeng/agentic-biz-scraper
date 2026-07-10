// Temporary migration script — converts v1 JSON definitions to v2 format
// Uses raw JSON manipulation (no internal type access needed)
using System.Text.Json;
using System.Text.Json.Nodes;

var defsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    "src", "BizScraper.Api", "Infrastructure", "Scraping", "Definitions");
defsDir = Path.GetFullPath(defsDir);

Console.WriteLine($"Definitions directory: {defsDir}");

foreach (var file in Directory.GetFiles(defsDir, "*.json"))
{
    var fileName = Path.GetFileName(file);
    var json = File.ReadAllText(file);
    var root = JsonNode.Parse(json)?.AsObject()
        ?? throw new InvalidOperationException($"Failed to parse {fileName}");

    var schemaVersion = root["schemaVersion"];
    if (schemaVersion is JsonValue val && val.TryGetValue<string>(out _))
    {
        Console.WriteLine($"SKIP {fileName} — already v2");
        continue;
    }

    // Build v2 object
    var v2 = new JsonObject();

    v2["schemaVersion"] = "1.0.0";

    // Metadata
    var metadata = new JsonObject
    {
        ["id"] = Guid.NewGuid().ToString("D"),
        ["name"] = root["name"]?.GetValue<string>(),
        ["state"] = root["state"]?.GetValue<string>(),
        ["endpoint"] = root["endpoint"]?.GetValue<string>()
    };
    v2["metadata"] = metadata;

    // Variables
    var v1Vars = root["variables"]?.AsArray() ?? [];
    var v2Vars = new JsonArray();
    foreach (var v in v1Vars)
    {
        var v2Var = new JsonObject
        {
            ["name"] = v?["name"]?.GetValue<string>(),
            ["source"] = v?["source"]?.GetValue<string>()
        };
        if (v?["required"] is JsonValue req)
        {
            v2Var["required"] = req.GetValue<bool>();
        }
        v2Vars.Add(v2Var);
    }
    v2["variables"] = v2Vars;

    // Actions
    var v1Actions = root["actions"]?.AsArray() ?? [];
    var counter = 0;
    v2["actions"] = MigrateActions(v1Actions, ref counter);

    // Output
    var v1Output = root["output"]?.AsObject();
    var v2Output = new JsonObject
    {
        ["variableName"] = v1Output?["variableName"]?.GetValue<string>(),
        ["type"] = v1Output?["type"]?.GetValue<string>()
    };
    v2["output"] = v2Output;

    // Write pretty
    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(file, v2.ToJsonString(options));
    Console.WriteLine($"MIGRATED {fileName}");
}

Console.WriteLine("Done.");

static JsonArray MigrateActions(JsonArray v1Actions, ref int counter)
{
    var result = new JsonArray();
    for (var i = 0; i < v1Actions.Count; i++)
    {
        var src = v1Actions[i]?.AsObject();
        if (src is null) continue;

        var globalIndex = counter++;
        var action = new JsonObject
        {
            ["id"] = $"action-{globalIndex:D3}",
            ["order"] = globalIndex
        };

        // Copy all properties, converting selector → target and condition
        foreach (var prop in src)
        {
            switch (prop.Key)
            {
                case "selector":
                    if (prop.Value is not null)
                    {
                        var target = new JsonObject
                        {
                            ["selectors"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["strategy"] = "css",
                                    ["value"] = prop.Value.DeepClone(),
                                    ["confidence"] = 1.0
                                }
                            }
                        };
                        action["target"] = target;
                    }
                    break;
                case "condition":
                    action["condition"] = MigrateCondition(prop.Value);
                    break;
                case "terminateWhen":
                    action["terminateWhen"] = MigrateCondition(prop.Value);
                    break;
                case "actions":
                    if (prop.Value is JsonArray subActions)
                    {
                        action["actions"] = MigrateActions(subActions, ref counter);
                    }
                    break;
                default:
                    action[prop.Key] = prop.Value?.DeepClone();
                    break;
            }
        }

        result.Add(action);
    }
    return result;
}

static JsonNode? MigrateCondition(JsonNode? condition)
{
    if (condition is null) return null;
    var obj = condition.AsObject();
    var result = new JsonObject();
    foreach (var prop in obj)
    {
        if (prop.Key == "condition" && prop.Value is JsonObject inner)
        {
            result["condition"] = MigrateCondition(inner);
        }
        else
        {
            result[prop.Key] = prop.Value?.DeepClone();
        }
    }
    return result;
}
