using BizScraper.Api.Common;

namespace BizScraper.UnitTests.Common;

public sealed class UniqueKeyEncoderTests
{
    [Theory]
    [InlineData("US-CO")]
    [InlineData("US-WY")]
    [InlineData("DE-DE")]
    public void Encode_Decode_Roundtrip(string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["param1"] = "value1",
            ["param2"] = "value2"
        };

        var encoded = UniqueKeyEncoder.Encode(state, parameters);
        Assert.True(UniqueKeyEncoder.TryDecode(encoded, out var decodedState, out var decodedParams, out _));
        Assert.Equal(state, decodedState);
        Assert.Equal("value1", decodedParams["param1"]);
        Assert.Equal("value2", decodedParams["param2"]);
    }

    [Fact]
    public void TryDecode_InvalidBase64_ReturnsError()
    {
        Assert.False(UniqueKeyEncoder.TryDecode("not-valid!!!", out _, out _, out var error));
        Assert.Contains("base64url", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_InvalidJson_ReturnsError()
    {
        // Valid base64url but invalid JSON
        var encoded = Convert.ToBase64String("not json"u8.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.False(UniqueKeyEncoder.TryDecode(encoded, out _, out _, out var error));
        Assert.Contains("JSON", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_UnknownState_ReturnsError()
    {
        var encoded = UniqueKeyEncoder.Encode("US-CO", new Dictionary<string, string> { ["a"] = "b" });
        // Manually create a payload with unknown state
        var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { state = "UNKNOWN", @params = new Dictionary<string, string> { ["a"] = "b" } });
        var badEncoded = Convert.ToBase64String(jsonBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.False(UniqueKeyEncoder.TryDecode(badEncoded, out _, out _, out var error));
        Assert.Contains("Invalid state prefix", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsError()
    {
        Assert.False(UniqueKeyEncoder.TryDecode("", out _, out _, out var error));
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encode_CoSearchParams_ProducesDecodableKey()
    {
        var parameters = new Dictionary<string, string>
        {
            ["masterFileId"] = "20201849877",
            ["fileId"] = "20201849877",
            ["entityId2"] = "20201849877"
        };

        var encoded = UniqueKeyEncoder.Encode("US-CO", parameters);
        Assert.True(UniqueKeyEncoder.TryDecode(encoded, out var state, out var decoded, out _));
        Assert.Equal("US-CO", state);
        Assert.Equal("20201849877", decoded["masterFileId"]);
    }
}
