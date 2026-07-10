using BizScraper.Api.Infrastructure.Scraping.Engine;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class BrowserPoolTests
{
    [Theory]
    [InlineData("local")]
    [InlineData("remote")]
    public void BrowserPool_Constructor_SetsMode(string mode)
    {
        using var pool = new BrowserPool(mode);
        Assert.Equal(mode, pool.Mode);
    }

    [Fact]
    public void BrowserPool_Initial_UsageCountIsZero()
    {
        using var pool = new BrowserPool("local");
        Assert.Equal(0, pool.UsageCount);
    }

    [Fact]
    public void BrowserPool_Initial_BrowserIsNull()
    {
        using var pool = new BrowserPool("local");
        Assert.False(pool.HasBrowser);
    }

    [Fact]
    public void BrowserPool_IncrementUsage_IncrementsCount()
    {
        using var pool = new BrowserPool("local");
        pool.IncrementUsage();
        Assert.Equal(1, pool.UsageCount);
    }

    [Fact]
    public void BrowserPool_NeedsRecycle_WhenUsageExceedsThreshold()
    {
        using var pool = new BrowserPool("local");
        for (var i = 0; i < 25; i++)
        {
            pool.IncrementUsage();
        }

        Assert.True(pool.NeedsRecycle(25));
    }

    [Fact]
    public void BrowserPool_DoesNotNeedRecycle_WhenBelowThreshold()
    {
        using var pool = new BrowserPool("local");
        for (var i = 0; i < 10; i++)
        {
            pool.IncrementUsage();
        }

        Assert.False(pool.NeedsRecycle(25));
    }

    [Fact]
    public void BrowserPool_ResetUsage_ResetsCount()
    {
        using var pool = new BrowserPool("local");
        for (var i = 0; i < 10; i++)
        {
            pool.IncrementUsage();
        }

        pool.ResetUsage();
        Assert.Equal(0, pool.UsageCount);
    }

    [Fact]
    public async Task BrowserPool_DisposeAsync_DoesNotThrow()
    {
        var pool = new BrowserPool("local");
        await pool.DisposeAsync();
    }

    [Fact]
    public async Task BrowserPool_AcquireLock_Serializes()
    {
        using var pool = new BrowserPool("local");
        await pool.AcquireLockAsync(CancellationToken.None);
        // Lock is held — cannot acquire again without release
        var acquired = pool.TryAcquireLockAsync(TimeSpan.FromMilliseconds(50));
        Assert.False(await acquired);
        pool.ReleaseLock();
    }

    [Fact]
    public async Task BrowserPool_ReleaseLock_AllowsReacquire()
    {
        using var pool = new BrowserPool("local");
        await pool.AcquireLockAsync(CancellationToken.None);
        pool.ReleaseLock();
        var acquired = pool.TryAcquireLockAsync(TimeSpan.FromMilliseconds(50));
        Assert.True(await acquired);
        pool.ReleaseLock();
    }
}
