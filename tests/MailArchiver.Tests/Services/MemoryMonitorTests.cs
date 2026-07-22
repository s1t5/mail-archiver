using MailArchiver.Services;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MemoryMonitor"/>.
/// These are smoke tests since the methods rely on process/GC state.
/// </summary>
public class MemoryMonitorTests
{
    [Fact]
    public void GetCurrentMemoryUsage_ReturnsNonNegative()
    {
        var usage = MemoryMonitor.GetCurrentMemoryUsage();
        Assert.True(usage >= 0);
    }

    [Fact]
    public void GetMemoryUsageFormatted_ContainsWorkingSetAndManaged()
    {
        var formatted = MemoryMonitor.GetMemoryUsageFormatted();
        Assert.Contains("Working Set:", formatted);
        Assert.Contains("Managed:", formatted);
    }

    [Fact]
    public void GetPeakMemoryUsageFormatted_ReturnsNonEmpty()
    {
        _ = MemoryMonitor.GetCurrentMemoryUsage();
        var peak = MemoryMonitor.GetPeakMemoryUsageFormatted();
        Assert.False(string.IsNullOrEmpty(peak));
    }

    [Fact]
    public void ForceGarbageCollection_DoesNotThrow()
        => MemoryMonitor.ForceGarbageCollection();
}