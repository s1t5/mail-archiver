using MailArchiver.Services.Shared;
using MimeKit;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="MailContentHelper.GenerateFallbackMessageIdCandidates"/>.
/// Regression tests for GitHub discussion #302: emails without a Message-ID header were
/// never deleted from the IMAP server because the retention-deletion pipeline computed a
/// different fallback key than the pipeline that archived the mail. The deletion side must
/// match every fallback key format that any pipeline/version has ever written:
///   K1 = current IMAP archiving format (hash incl. canonical headers, full date fallback chain)
///   K2 = EML/MBOX import format (hash without canonical headers, Date header only)
///   K3 = legacy IMAP archiving format ("{From}-{To}-{Subject}-{ticks}")
/// The synthetic messages replicate the three real-world mails provided by the reporter:
/// plain subject with Date header, missing Date header, and folded encoded-word subject.
/// </summary>
public class MailContentHelperFallbackCandidatesTests
{
    private static MimeMessage LoadRawMessage(string raw)
        => MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)));

    /// <summary>
    /// Computes the deletion-side candidates exactly like ImapMailSyncService.DeleteOldEmailsAsync:
    /// bare addresses and decoded subject (as delivered by the IMAP ENVELOPE), the Date header
    /// value (envelope date, default when missing) and the raw headers.
    /// </summary>
    private static List<string> DeleteSideCandidates(MimeMessage msg)
    {
        var from = string.Join(",", msg.From.Mailboxes.Select(m => m.Address));
        var to = string.Join(",", msg.To.Mailboxes.Select(m => m.Address));
        return MailContentHelper.GenerateFallbackMessageIdCandidates(
            from, to, msg.Subject,
            msg.Date, // envelope date == Date header; default(DateTimeOffset) when missing
            msg.Headers,
            msg.From.ToString(), msg.To.ToString());
    }

    /// <summary>K1: key written by EmailCoreService.ArchiveEmailAsync (current IMAP format).</summary>
    private static string ImapArchiveKey(MimeMessage msg)
    {
        var from = string.Join(",", msg.From.Mailboxes.Select(m => m.Address));
        var to = string.Join(",", msg.To.Mailboxes.Select(m => m.Address));
        var emailDate = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);
        return MailContentHelper.GenerateFallbackMessageId(
            from, to, msg.Subject, emailDate.Ticks,
            MailContentHelper.BuildCanonicalHeaders(msg.Headers));
    }

    /// <summary>K2: key written by MailImporter.GenerateMessageId (EML/MBOX import format).</summary>
    private static string ImportKey(MimeMessage msg)
    {
        var from = string.Join(",", msg.From.Mailboxes.Select(m => m.Address));
        var to = string.Join(",", msg.To.Mailboxes.Select(m => m.Address));
        return MailContentHelper.GenerateFallbackMessageId(from, to, msg.Subject, msg.Date.Ticks);
    }

    /// <summary>K3: key written by the IMAP archiving path before the deterministic generator existed.</summary>
    private static string LegacyImapKey(MimeMessage msg)
    {
        var emailDate = MailContentHelper.ExtractEmailDate(msg.Date, msg.Headers);
        return $"{msg.From}-{msg.To}-{msg.Subject}-{emailDate.Ticks}";
    }

    [Fact]
    public void Candidates_PlainSubjectWithDateHeader_ContainAllWrittenFormats()
    {
        // Class of reporter mail #1 (NAS warning, 2016): plain ASCII subject, Date header,
        // display names on the addresses, no Message-ID.
        var msg = LoadRawMessage(
            "Return-Path: <nas@example.local>\r\n" +
            "Received: from NAS (dynamic.example.net [192.0.2.10])\r\n" +
            "\tby mx.example.local (Postfix) with ESMTPSA id ABC123\r\n" +
            "\tfor <itlog@example.local>; Wed, 30 Nov 2016 14:36:32 +0100 (CET)\r\n" +
            "Received: by NAS (sSMTP sendmail emulation); Wed, 30 Nov 2016 14:36:28 +0100\r\n" +
            "Date: Wed, 30 Nov 2016 14:36:28 +0100\r\n" +
            "To: \"itlog\" <itlog@example.local>\r\n" +
            "Subject: NAS Warning\r\n" +
            "From: \"nas-monitor\" <nas@example.local>\r\n\r\nServer Name: NAS\r\n");

        var candidates = DeleteSideCandidates(msg);

        Assert.Contains(ImapArchiveKey(msg), candidates);
        Assert.Contains(ImportKey(msg), candidates);
        Assert.Contains(LegacyImapKey(msg), candidates);
    }

    [Fact]
    public void Candidates_MissingDateHeader_ContainAllWrittenFormats()
    {
        // Class of reporter mail #2 (toner notification): no Message-ID AND no Date header.
        // The deletion side previously fell back to the IMAP INTERNALDATE here while the
        // archiving side used the Received header date - the keys diverged and the mail was
        // never deleted. The candidates must be based on the Received fallback instead.
        var msg = LoadRawMessage(
            "Return-Path: <printer@example.local>\r\n" +
            "Received: from mx.example.local\r\n" +
            "\tby mx.example.local with LMTP id XYZ\r\n" +
            "\tfor <web@example.local>; Thu, 09 Jan 2020 13:41:46 +0100\r\n" +
            "Received: from PRINTER (office.example.net [192.0.2.20])\r\n" +
            "\tby mx.example.local (Postfix) with ESMTPA id DEF456\r\n" +
            "\tfor <itlog@example.local>; Thu,  9 Jan 2020 13:41:46 +0100 (CET)\r\n" +
            "From: printer@example.local\r\n" +
            "To: itlog@example.local\r\n" +
            "Subject: =?ISO-8859-1?Q?Notification [Low Toner BK]?=\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: text/plain; charset=\"ISO-8859-1\"\r\n\r\nbody\r\n");

        Assert.Equal(default, msg.Date); // sanity: no Date header

        var candidates = DeleteSideCandidates(msg);

        Assert.Contains(ImapArchiveKey(msg), candidates);
        Assert.Contains(ImportKey(msg), candidates);
        Assert.Contains(LegacyImapKey(msg), candidates);
    }

    [Fact]
    public void Candidates_MissingDateHeader_ReceivedDateIsUsedForKeys()
    {
        // The K1/K3 candidates must embed the Received-header date (full fallback chain),
        // not 0/MinValue - this is what makes them match rows written by the archiving path.
        var msg = LoadRawMessage(
            "Received: from PRINTER by mx.example.local (Postfix) with ESMTPA id DEF456\r\n" +
            "\tfor <itlog@example.local>; Thu,  9 Jan 2020 13:41:46 +0100 (CET)\r\n" +
            "From: printer@example.local\r\nTo: itlog@example.local\r\nSubject: t\r\n\r\nbody\r\n");

        var receivedDate = new DateTimeOffset(2020, 1, 9, 13, 41, 46, TimeSpan.FromHours(1));
        var expectedK1 = MailContentHelper.GenerateFallbackMessageId(
            "printer@example.local", "itlog@example.local", "t", receivedDate.Ticks,
            MailContentHelper.BuildCanonicalHeaders(msg.Headers));
        var expectedK3 = $"printer@example.local-itlog@example.local-t-{receivedDate.Ticks}";

        var candidates = DeleteSideCandidates(msg);

        Assert.Contains(expectedK1, candidates);
        Assert.Contains(expectedK3, candidates);
    }

    [Fact]
    public void Candidates_FoldedEncodedWordSubject_ContainAllWrittenFormats()
    {
        // Class of reporter mail #3 (firmware notification): no Message-ID, Date header,
        // subject folded across two RFC 2047 encoded words (UTF-8).
        var msg = LoadRawMessage(
            "Received: from NAS-Zeus (office.example.net [192.0.2.30])\r\n" +
            "\tby mx.example.local (Postfix) with ESMTPSA id GHI789\r\n" +
            "\tfor <itlog@example.local>; Mon,  1 May 2023 00:00:20 +0200 (CEST)\r\n" +
            "Date: Sun, 30 Apr 2023 22:00:00 GMT\r\n" +
            "From: nas@example.local\r\n" +
            "Subject: =?UTF-8?q?=5BInfo=5D=5BFirmware=5D_Notification_?=\r\n" +
            " =?UTF-8?q?von_Ihrem_Ger=C3=A4t=3A_NAS-Zeus?=\r\n" +
            "To: <itlog@example.local>\r\n" +
            "Mime-Version: 1.0\r\n\r\nbody\r\n");

        Assert.Equal("[Info][Firmware] Notification von Ihrem Gerät: NAS-Zeus", msg.Subject); // sanity

        var candidates = DeleteSideCandidates(msg);

        Assert.Contains(ImapArchiveKey(msg), candidates);
        Assert.Contains(ImportKey(msg), candidates);
        Assert.Contains(LegacyImapKey(msg), candidates);
    }

    [Fact]
    public void Candidates_DifferentMessage_KeysDoNotCrossMatch()
    {
        // Sanity: the candidate set of one message must not accidentally contain the
        // archiving key of a different message (same addresses, different subject).
        var msg1 = LoadRawMessage(
            "Date: Wed, 30 Nov 2016 14:36:28 +0100\r\nFrom: a@x.com\r\nTo: b@x.com\r\nSubject: One\r\n\r\nbody");
        var msg2 = LoadRawMessage(
            "Date: Wed, 30 Nov 2016 14:36:28 +0100\r\nFrom: a@x.com\r\nTo: b@x.com\r\nSubject: Two\r\n\r\nbody");

        var candidates1 = DeleteSideCandidates(msg1);

        Assert.DoesNotContain(ImapArchiveKey(msg2), candidates1);
    }

    [Fact]
    public void Candidates_AreDistinct()
    {
        var msg = LoadRawMessage(
            "Date: Wed, 30 Nov 2016 14:36:28 +0100\r\nFrom: a@x.com\r\nTo: b@x.com\r\nSubject: s\r\n\r\nbody");

        var candidates = DeleteSideCandidates(msg);

        Assert.Equal(candidates.Count, candidates.Distinct().Count());
    }
}
