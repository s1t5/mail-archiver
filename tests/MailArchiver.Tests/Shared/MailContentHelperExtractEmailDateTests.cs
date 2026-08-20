using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for the shared date extraction used by both the archiving pipeline
/// (EmailCoreService) and the retention-deletion pipeline (ImapMailSyncService).
/// Both must compute identical dates for the same message, otherwise the fallback
/// Message-IDs diverge and archived mails without Message-ID header are never
/// deleted from the server (GitHub discussion #302).
/// </summary>
public class MailContentHelperExtractEmailDateTests
{
    private static MimeMessage LoadRawMessage(string raw)
        => MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)));

    [Fact]
    public void Extract_DateHeaderPresent_ReturnsDateHeader()
    {
        var msg = LoadRawMessage(
            "From: a@x.com\r\nTo: b@x.com\r\n" +
            "Date: Wed, 30 Nov 2016 14:36:28 +0100\r\n\r\nbody");

        var result = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);

        Assert.Equal(msg.Date, result);
        Assert.Equal(new DateTimeOffset(2016, 11, 30, 14, 36, 28, TimeSpan.FromHours(1)), result);
    }

    [Fact]
    public void Extract_NoDateHeader_FallsBackToOldestReceivedHeader()
    {
        // Mirrors the reporter's mail without Date header: the date must come from the
        // Received chain (oldest hop = last Received header in the list), not from the
        // IMAP INTERNALDATE (which changes when messages are moved between folders).
        var msg = LoadRawMessage(
            "Received: from mx.example.local by mx.example.local with LMTP id XYZ;\r\n" +
            "\tThu, 09 Jan 2020 13:41:46 +0100\r\n" +
            "Received: from PRINTER (office.example.net [192.0.2.20])\r\n" +
            "\tby mx.example.local (Postfix) with ESMTPA id DEF456\r\n" +
            "\tfor <itlog@example.local>; Thu,  9 Jan 2020 13:41:46 +0100 (CET)\r\n" +
            "From: printer@example.local\r\nTo: itlog@example.local\r\n\r\nbody");

        Assert.Equal(default, msg.Date); // sanity: MimeKit has no Date header value

        var result = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);

        Assert.Equal(new DateTimeOffset(2020, 1, 9, 13, 41, 46, TimeSpan.FromHours(1)), result);
    }

    [Fact]
    public void Extract_NoDateNoReceived_FallsBackToResentDate()
    {
        var msg = LoadRawMessage(
            "Resent-Date: Mon, 05 Jan 2004 10:00:00 +0100\r\n" +
            "From: a@x.com\r\nTo: b@x.com\r\n\r\nbody");

        var result = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);

        Assert.Equal(new DateTimeOffset(2004, 1, 5, 10, 0, 0, TimeSpan.FromHours(1)), result);
    }

    [Fact]
    public void Extract_NoDateSourceAtAll_ReturnsMinValue()
    {
        var msg = LoadRawMessage("From: a@x.com\r\nTo: b@x.com\r\n\r\nbody");

        var result = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);

        Assert.Equal(DateTimeOffset.MinValue, result);
    }

    [Fact]
    public void Extract_NullHeaders_WithDefaultDate_ReturnsMinValue()
    {
        var result = MailContentHelper.ExtractEmailDate(default, null);

        Assert.Equal(DateTimeOffset.MinValue, result);
    }

    [Fact]
    public void ExtractDateFromReceivedHeader_StripsTrailingComment()
    {
        var result = MailContentHelper.ExtractDateFromReceivedHeader(
            "from PRINTER by mx.example.local (Postfix) with ESMTPA id DEF456 for <itlog@example.local>; Thu,  9 Jan 2020 13:41:46 +0100 (CET)");

        Assert.Equal(new DateTimeOffset(2020, 1, 9, 13, 41, 46, TimeSpan.FromHours(1)), result);
    }

    [Fact]
    public void ExtractDateFromReceivedHeader_NoSemicolon_ReturnsNull()
    {
        Assert.Null(MailContentHelper.ExtractDateFromReceivedHeader("from mx by hub (no date at all)"));
    }

    [Fact]
    public void ExtractDateFromReceivedHeader_Empty_ReturnsNull()
    {
        Assert.Null(MailContentHelper.ExtractDateFromReceivedHeader(""));
        Assert.Null(MailContentHelper.ExtractDateFromReceivedHeader(null!));
    }

    [Fact]
    public void ParseDateHeaderValue_Unparseable_ReturnsNull()
    {
        Assert.Null(MailContentHelper.ParseDateHeaderValue("not a date"));
    }
}
