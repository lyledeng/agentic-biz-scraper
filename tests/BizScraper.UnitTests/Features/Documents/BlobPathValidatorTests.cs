using BizScraper.Api.Features.Documents.Validation;

namespace BizScraper.UnitTests.Features.Documents;

public sealed class BlobPathValidatorTests
{
    [Theory]
    [InlineData("abc123/file.pdf")]
    [InlineData("correlation-id/doc_name.pdf")]
    [InlineData("A1B2C3/report-2024.pdf")]
    [InlineData("id/file_name-v2.pdf")]
    [InlineData("2026-04-01/us-co-business-search/test-company/abc123/file.pdf")]
    [InlineData("2026-04-01/us-wy-entity-details/entity/corr123/cert.pdf")]
    [InlineData("a/b/c")]
    [InlineData("folder/sub/file.pdf")]
    public void IsValid_ValidPaths_ReturnsTrue(string path) =>
        Assert.True(BlobPathValidator.IsValid(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NullOrWhitespace_ReturnsFalse(string? path) =>
        Assert.False(BlobPathValidator.IsValid(path));

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    [InlineData("folder/../secret")]
    public void IsValid_PathTraversal_ReturnsFalse(string path) =>
        Assert.False(BlobPathValidator.IsValid(path));

    [Theory]
    [InlineData("/leading-slash/file.pdf")]
    [InlineData("no-slash")]
    public void IsValid_InvalidFormat_ReturnsFalse(string path) =>
        Assert.False(BlobPathValidator.IsValid(path));

    [Theory]
    [InlineData("a/b/c/d/e/f/g")]
    public void IsValid_TooManySegments_ReturnsFalse(string path) =>
        Assert.False(BlobPathValidator.IsValid(path));

    [Theory]
    [InlineData("folder/file name.pdf")]
    [InlineData("folder/file@name.pdf")]
    [InlineData("fol der/file.pdf")]
    [InlineData("folder/file#name.pdf")]
    public void IsValid_BadCharacters_ReturnsFalse(string path) =>
        Assert.False(BlobPathValidator.IsValid(path));

    [Fact]
    public void IsValid_TooLong_ReturnsFalse()
    {
        var longPath = new string('a', 500) + "/" + new string('b', 525);
        Assert.False(BlobPathValidator.IsValid(longPath));
    }

    [Fact]
    public void IsValid_ExactlyAtMaxLength_ReturnsTrue()
    {
        // 1024 chars total: 511 + "/" + 512
        var path = new string('a', 511) + "/" + new string('b', 512);
        Assert.True(BlobPathValidator.IsValid(path));
    }
}
