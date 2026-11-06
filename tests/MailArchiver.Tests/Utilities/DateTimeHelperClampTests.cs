using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Utilities;

/// <summary>
/// Tests for the plausible-range clamp on <c>ArchivedEmail.SentDate</c>.
/// <para>
/// Motivation (GitHub issue #600): a <c>Date:</c> header MimeKit cannot represent collapses to
/// <see cref="DateTimeOffset.MinValue"/>, i.e. year 1. The duplicate query subtracts the stored
/// <c>SentDate</c> from that value, and PostgreSQL's <c>interval</c> type spans fewer years than
/// <c>timestamp</c>, so the subtraction raises <c>22008: interval out of range</c> and kills the
/// whole import job. The clamp keeps both sides of that comparison inside a range where the
/// difference can never overflow.
/// </para>
/// </summary>
public class DateTimeHelperClampTests
{
    private static DateTimeHelper Create(string tzId = "Europe/Berlin")
        => new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = tzId }));

    private static MimeMessage LoadRawMessage(string raw)
        => MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)));

    [Fact]
    public void Clamp_YearOne_IsRaisedToLowerBound()
    {
        var clamped = DateTimeHelper.ClampToPlausibleRange(new DateTime(1, 1, 1, 0, 0, 0));

        Assert.Equal(DateTimeHelper.MinimumPlausibleSentDate, clamped);
        Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0), clamped);
    }

    [Fact]
    public void Clamp_FarFuture_IsLoweredToUpperBound()
    {
        var clamped = DateTimeHelper.ClampToPlausibleRange(new DateTime(9999, 12, 31, 23, 59, 59));

        Assert.Equal(DateTimeHelper.MaximumPlausibleSentDate, clamped);
        Assert.Equal(new DateTime(2100, 12, 31, 23, 59, 59), clamped);
    }

    [Theory]
    [InlineData(1970, 1, 1, 0, 0, 0)]
    [InlineData(1999, 6, 15, 12, 30, 0)]
    [InlineData(2026, 11, 6, 10, 0, 0)]
    [InlineData(2100, 12, 31, 23, 59, 59)]
    public void Clamp_InsideRange_IsUnchanged(int y, int mo, int d, int h, int mi, int s)
    {
        var value = new DateTime(y, mo, d, h, mi, s, DateTimeKind.Unspecified);

        Assert.Equal(value, DateTimeHelper.ClampToPlausibleRange(value));
    }

    [Fact]
    public void ConvertToDisplayTimeZone_DateTimeOffsetMinValue_IsClamped()
    {
        // This is what MimeKit hands over for an unparsable or out-of-range Date header.
        var result = Create().ConvertToDisplayTimeZone(DateTimeOffset.MinValue);

        Assert.Equal(DateTimeHelper.MinimumPlausibleSentDate, result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Fact]
    public void ConvertToDisplayTimeZone_DateTimeOffsetMaxValue_IsClamped()
    {
        var result = Create().ConvertToDisplayTimeZone(DateTimeOffset.MaxValue);

        Assert.Equal(DateTimeHelper.MaximumPlausibleSentDate, result);
    }

    [Theory]
    [InlineData("Fri, 01 Jan 100000 00:00:00 +0000")]
    [InlineData("Sat, 01 Jan 294276 00:00:00 +0000")]
    [InlineData("0000-00-00 00:00:00")]
    [InlineData("Tue, 30 Feb 2024 10:00:00 +0000")]
    public void MalformedDateHeader_NeverProducesAnOutOfRangeSentDate(string dateHeader)
    {
        // End-to-end over the path MailImporter takes: parse the header, run it through the
        // shared fallback chain, convert it the way the write path does.
        var msg = LoadRawMessage(
            "From: a@x.com\r\nTo: b@x.com\r\nSubject: t\r\nDate: " + dateHeader + "\r\n\r\nbody");

        var sentDate = Create().ConvertToDisplayTimeZone(
            MailArchiver.Services.Shared.MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers));

        Assert.InRange(sentDate, DateTimeHelper.MinimumPlausibleSentDate, DateTimeHelper.MaximumPlausibleSentDate);
    }

    [Fact]
    public void MalformedDateHeader_FallsBackToReceivedHeaderNotYearOne()
    {
        // With a Received header present the fallback chain finds a real date, so the clamp is
        // not even needed - the point is that the stored value is the real one, not 1970.
        var msg = LoadRawMessage(
            "From: a@x.com\r\nTo: b@x.com\r\nSubject: t\r\n" +
            "Date: Sat, 01 Jan 294276 00:00:00 +0000\r\n" +
            "Received: from mx.example by mx.example; Wed, 05 Mar 2014 08:12:00 +0100\r\n" +
            "\r\nbody");

        var sentDate = Create().ConvertToDisplayTimeZone(
            MailArchiver.Services.Shared.MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers));

        Assert.Equal(new DateTime(2014, 3, 5, 8, 12, 0), sentDate);
    }

    [Fact]
    public void ClampedBounds_StayFarInsidePostgresIntervalRange()
    {
        // PostgreSQL raises "22008: interval out of range" once a timestamp difference exceeds
        // about 292,271 years. The clamp has to keep the widest possible difference, from the
        // lowest to the highest allowed SentDate, far below that.
        var span = (DateTimeHelper.MaximumPlausibleSentDate - DateTimeHelper.MinimumPlausibleSentDate).TotalDays;

        Assert.True(span / 365.25 < 1000,
            $"Clamp range spans {span / 365.25:F0} years, which defeats the purpose of the guard.");
    }
}
