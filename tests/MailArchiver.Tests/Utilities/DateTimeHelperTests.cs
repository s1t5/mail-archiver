using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using Xunit;

namespace MailArchiver.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="DateTimeHelper"/>.
/// </summary>
public class DateTimeHelperTests
{
    private static DateTimeHelper Create(string tzId = "Europe/Berlin")
        => new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = tzId }));

    [Theory]
    [InlineData(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc)]
    [InlineData(2024, 12, 15, 10, 0, 0, DateTimeKind.Utc)]
    public void ConvertToDisplayTimeZone_FromUtc_ConvertsCorrectly(int y, int mo, int d, int h, int mi, int s, DateTimeKind kind)
    {
        var helper = Create();
        var utc = new DateTime(y, mo, d, h, mi, s, kind);
        var result = helper.ConvertToDisplayTimeZone(utc);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var expected = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToDisplayTimeZone_DateTimeOffset_Converts()
    {
        var helper = Create();
        var dto = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var result = helper.ConvertToDisplayTimeZone(dto);
        var expected = TimeZoneInfo.ConvertTime(dto, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToDisplayTimeZone_UnspecifiedKind_ReturnsAsIs()
    {
        var helper = Create();
        var dt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var result = helper.ConvertToDisplayTimeZone(dt);
        Assert.Equal(dt, result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Fact]
    public void ConvertToDisplayTimeZone_LocalKind_ConvertsToDisplayTz()
    {
        var helper = Create();
        var local = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Local);
        var result = helper.ConvertToDisplayTimeZone(local);
        var expected = TimeZoneInfo.ConvertTime(local, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified, 2024, 6, 15, 10, 0, 0)]
    [InlineData(DateTimeKind.Utc, 2024, 6, 15, 10, 0, 0)]
    public void EnsureUtc_ConvertsUnspecifiedAndLocalToUtc(DateTimeKind kind, int y, int mo, int d, int h, int mi, int s)
    {
        var dt = new DateTime(y, mo, d, h, mi, s, kind);
        var result = DateTimeHelper.EnsureUtc(dt);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void EnsureUtc_AlreadyUtc_ReturnsAsIs()
    {
        var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(dt, DateTimeHelper.EnsureUtc(dt));
    }

    [Fact]
    public void EnsureUtc_Local_ConvertedToUtc()
    {
        var local = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);
        var result = DateTimeHelper.EnsureUtc(local);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(local.ToUniversalTime(), result);
    }

    [Fact]
    public void ToDisplayTimeZoneOffset_ReturnsCorrectOffset()
    {
        var helper = Create();
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var offset = helper.ToDisplayTimeZoneOffset(dt);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var expectedOffset = tz.GetUtcOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified));
        Assert.Equal(expectedOffset, offset.Offset);
        Assert.Equal(dt, offset.DateTime);
    }

    [Fact]
    public void ConvertFromDisplayTimeZoneToUtc_Unspecified_Converts()
    {
        var helper = Create();
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Unspecified);
        var result = helper.ConvertFromDisplayTimeZoneToUtc(dt);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        Assert.Equal(TimeZoneInfo.ConvertTimeToUtc(dt, tz), result);
    }

    [Fact]
    public void ConvertFromDisplayTimeZoneToUtc_AlreadyUtc_PassesThrough()
    {
        var helper = Create();
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(dt, helper.ConvertFromDisplayTimeZoneToUtc(dt));
    }
}