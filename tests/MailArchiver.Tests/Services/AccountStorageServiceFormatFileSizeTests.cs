using MailArchiver.Services;
using System.Globalization;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AccountStorageService.FormatFileSize"/>.
/// </summary>
public class AccountStorageServiceFormatFileSizeTests
{
    private static string Expected(decimal value, string unit)
        => string.Format(CultureInfo.CurrentCulture, "{0:n1} {1}", value, unit);

    [Theory]
    [InlineData(0, "B", 0)]
    [InlineData(500, "B", 500)]
    [InlineData(511, "B", 511)]
    [InlineData(1024, "KB", 1)]
    [InlineData(1536, "KB", 1.5)]
    [InlineData(1048576, "MB", 1)]
    [InlineData(1073741824, "GB", 1)]
    [InlineData(1099511627776L, "TB", 1)]
    [InlineData(1125899906842624L, "PB", 1)]
    public void FormatFileSize_FormatsCorrectly(long bytes, string unit, decimal value)
        => Assert.Equal(Expected(value, unit), AccountStorageService.FormatFileSize(bytes));

    [Fact]
    public void FormatFileSize_OneByte_FormatsWithCulture()
    {
        var result = AccountStorageService.FormatFileSize(1);
        var expected = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:n1} B", 1m);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatFileSize_LargeValue_StaysAtPetabyte()
    {
        var result = AccountStorageService.FormatFileSize(long.MaxValue);
        Assert.EndsWith(" PB", result);
    }
}