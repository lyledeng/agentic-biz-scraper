using System.Text.Json;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Common.Serialization;

namespace BizScraper.UnitTests.Common.Models;

public sealed class IowaNameEntryTests
{
    [Fact]
    public void IowaNameEntry_SerializesWithCamelCase()
    {
        var entry = new IowaNameEntry
        {
            Name = "ACME LLC",
            Type = "Legal",
            Status = "Active",
            Modified = false
        };

        var json = JsonSerializer.Serialize(entry, AppJsonSerializerContext.Default.IowaNameEntry);

        Assert.Contains("\"name\":\"ACME LLC\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Legal\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Active\"", json, StringComparison.Ordinal);
        Assert.Contains("\"modified\":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IowaNameEntry_RoundTripsViaSourceGenContext()
    {
        var original = new IowaNameEntry
        {
            Name = "ACME CORP",
            Type = "Fictitious",
            Status = "Inactive",
            Modified = true
        };

        var json = JsonSerializer.Serialize(original, AppJsonSerializerContext.Default.IowaNameEntry);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.IowaNameEntry);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Type, deserialized.Type);
        Assert.Equal(original.Status, deserialized.Status);
        Assert.Equal(original.Modified, deserialized.Modified);
    }
}
