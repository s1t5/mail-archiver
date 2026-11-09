using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The update hint depends on ranking release tags correctly: a string comparison puts
/// 2611.10 below 2611.9, and a tag that is not a version at all must never produce a hint.
/// </summary>
public class AppVersionComparerTests
{
    [Theory]
    [InlineData("2611.2", "2612.0", -1)]
    [InlineData("2612.0", "2611.2", 1)]
    [InlineData("2611.2", "2611.2", 0)]
    [InlineData("v2611.2", "2611.2", 0)]
    [InlineData("2611.2+abc123", "2611.2", 0)]
    [InlineData("2611.2-beta", "2611.2", 0)]
    [InlineData("2611.10", "2611.9", 1)]
    [InlineData("2611", "2611.0", 0)]
    [InlineData("2611.0.1", "2611", 1)]
    public void TryCompare_OrdersVersionsBySegment(string left, string right, int expected)
    {
        Assert.True(AppVersionComparer.TryCompare(left, right, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "2611.2")]
    [InlineData("", "2611.2")]
    [InlineData("   ", "2611.2")]
    [InlineData("dev", "2611.2")]
    [InlineData("2611.2", "dev")]
    [InlineData("main", "2611.2")]
    public void TryCompare_NonNumericVersion_IsNotComparable(string? left, string? right)
    {
        Assert.False(AppVersionComparer.TryCompare(left, right, out var result));
        Assert.Equal(0, result);
    }
}
