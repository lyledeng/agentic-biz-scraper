using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BizScraper.Api.Common;

/// <summary>
/// Encodes and decodes opaque uniqueKey tokens used for entity detail lookups.
/// </summary>
public static partial class UniqueKeyEncoder
{
    /// <summary>
    /// Encode a state + params payload into a base64url string.
    /// </summary>
    public static string Encode(string state, Dictionary<string, string> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentNullException.ThrowIfNull(parameters);

        var payload = new UniqueKeyPayload { State = state, Params = parameters };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, UniqueKeyJsonContext.Default.UniqueKeyPayload);
        return Base64UrlEncode(json);
    }

    /// <summary>
    /// Decode a base64url uniqueKey string, validating the state value.
    /// </summary>
    public static bool TryDecode(string uniqueKey, [NotNullWhen(true)] out string? state, [NotNullWhen(true)] out Dictionary<string, string>? parameters, [NotNullWhen(false)] out string? error)
    {
        state = null;
        parameters = null;
        error = null;

        if (string.IsNullOrWhiteSpace(uniqueKey))
        {
            error = "The uniqueKey parameter is required.";
            return false;
        }

        byte[] jsonBytes;
        try
        {
            jsonBytes = Base64UrlDecode(uniqueKey);
        }
        catch (FormatException)
        {
            error = "The uniqueKey value is not valid base64url.";
            return false;
        }

        UniqueKeyPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(jsonBytes, UniqueKeyJsonContext.Default.UniqueKeyPayload);
        }
        catch (JsonException)
        {
            error = "The uniqueKey value contains invalid JSON.";
            return false;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.State))
        {
            error = "The uniqueKey payload is missing the 'state' field.";
            return false;
        }

        if (!StatePrefixPattern().IsMatch(payload.State))
        {
            error = $"Invalid state prefix '{payload.State}' in uniqueKey. Expected format: XX-XX (e.g., US-CO, DE-DE).";
            return false;
        }

        state = payload.State;
        parameters = payload.Params ?? [];
        return true;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    [GeneratedRegex(@"^[A-Z]{2}-[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex StatePrefixPattern();
}

internal sealed class UniqueKeyPayload
{
    public string State { get; set; } = string.Empty;
    public Dictionary<string, string>? Params { get; set; }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(UniqueKeyPayload))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class UniqueKeyJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
