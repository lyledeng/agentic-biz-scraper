using System.Text.Json;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Common.Serialization;

namespace BizScraper.UnitTests.Common.Models;

public sealed class DetailSectionIowaFieldsTests
{
    [Fact]
    public void DetailSection_IowaFields_NullWhenUnset()
    {
        var section = new DetailSection
        {
            Name = "Test Entity",
            Identifier = "12345",
            Status = "Active"
        };

        var json = JsonSerializer.Serialize(section, AppJsonSerializerContext.Default.DetailSection);

        Assert.Contains("\"chapterCode\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"certificateNote\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"iowaNames\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailSection_IowaFields_SerializeWhenPopulated()
    {
        var section = new DetailSection
        {
            Name = "AACME LLC",
            Identifier = "635318",
            Status = "Active",
            ChapterCode = "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY",
            CertificateNote = "Certificate via paywall",
            IowaNames =
            [
                new IowaNameEntry
                {
                    Name = "AACME LLC",
                    Type = "Legal",
                    Status = "Active",
                    Modified = false
                }
            ]
        };

        var json = JsonSerializer.Serialize(section, AppJsonSerializerContext.Default.DetailSection);

        Assert.Contains("\"chapterCode\":\"CODE 489 DOMESTIC LIMITED LIABILITY COMPANY\"", json, StringComparison.Ordinal);
        Assert.Contains("\"certificateNote\":\"Certificate via paywall\"", json, StringComparison.Ordinal);
        Assert.Contains("\"iowaNames\":[", json, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"AACME LLC\"", json, StringComparison.Ordinal);
    }
}
