using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Shared;

public class RawMessageCodecTests
{
    // Folded header, encoded word, a DKIM-style signature and a base64 attachment: the
    // parts a re-serialization would most likely disturb.
    private const string SampleMessage =
        "Received: from mx.example.org (mx.example.org [192.0.2.1])\r\n" +
        "\tby mail.example.com with ESMTPS id abc123\r\n" +
        "\tfor <b@example.com>; Sat, 24 Oct 2026 10:00:00 +0200\r\n" +
        "DKIM-Signature: v=1; a=rsa-sha256; c=relaxed/relaxed; d=example.org; s=s1;\r\n" +
        " h=from:to:subject:date; bh=47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=;\r\n" +
        " b=AAAA\r\n" +
        "From: =?utf-8?q?J=C3=BCrgen?= <a@example.org>\r\n" +
        "To: b@example.com\r\n" +
        "Subject: =?utf-8?b?R3LDvMOfZQ==?=\r\n" +
        "Date: Sat, 24 Oct 2026 10:00:00 +0200\r\n" +
        "Message-ID: <raw-codec-test@example.org>\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: multipart/mixed; boundary=\"b1\"\r\n" +
        "\r\n" +
        "--b1\r\n" +
        "Content-Type: text/plain; charset=iso-8859-1\r\n" +
        "Content-Transfer-Encoding: quoted-printable\r\n" +
        "\r\n" +
        "Gr=FC=DFe=20\r\n" +
        "--b1\r\n" +
        "Content-Type: application/octet-stream; name=\"a.bin\"\r\n" +
        "Content-Disposition: attachment; filename=\"a.bin\"\r\n" +
        "Content-Transfer-Encoding: base64\r\n" +
        "\r\n" +
        "AAECAwQFBgcICQ==\r\n" +
        "--b1--\r\n";

    private static RawMessageOptions Enabled(int maxMb = 50) => new() { Enabled = true, MaxMessageSizeMb = maxMb };

    [Fact]
    public void Create_Disabled_ReturnsNull()
    {
        var capture = new RawMessageCapture(Encoding.ASCII.GetBytes(SampleMessage), RawMessageSource.Imap);
        Assert.Null(RawMessageCodec.Create(capture, new RawMessageOptions { Enabled = false }));
    }

    [Fact]
    public void Create_NoCapture_ReturnsNull()
    {
        Assert.Null(RawMessageCodec.Create(null, Enabled()));
        Assert.Null(RawMessageCodec.Create(new RawMessageCapture(Array.Empty<byte>(), RawMessageSource.Imap), Enabled()));
    }

    [Fact]
    public void Create_OverSizeLimit_ReturnsNull()
    {
        var bytes = new byte[1024 * 1024 + 1];
        Assert.Null(RawMessageCodec.Create(new RawMessageCapture(bytes, RawMessageSource.Imap), Enabled(maxMb: 1)));
        Assert.NotNull(RawMessageCodec.Create(new RawMessageCapture(bytes, RawMessageSource.Imap), Enabled(maxMb: 0)));
    }

    [Fact]
    public void Create_ThenDecompress_RoundTripsBytesAndHash()
    {
        var bytes = Encoding.ASCII.GetBytes(SampleMessage);
        var raw = RawMessageCodec.Create(new RawMessageCapture(bytes, RawMessageSource.EmlFile), Enabled())!;

        Assert.Equal(bytes.LongLength, raw.Size);
        Assert.Equal(RawMessageSource.EmlFile, raw.Source);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), raw.Sha256);
        Assert.Equal(bytes, RawMessageCodec.Decompress(raw));
    }

    [Fact]
    public void Serialize_UnmodifiedParsedMessage_ReproducesOriginalBytes()
    {
        var bytes = Encoding.ASCII.GetBytes(SampleMessage);
        using var message = MimeMessage.Load(new MemoryStream(bytes));

        Assert.Equal(SampleMessage, Encoding.ASCII.GetString(RawMessageCodec.Serialize(message)));
    }

    [Fact]
    public void Serialize_LfInput_IsWrittenWithCrlf()
    {
        var lf = SampleMessage.Replace("\r\n", "\n");
        using var message = MimeMessage.Load(new MemoryStream(Encoding.ASCII.GetBytes(lf)));

        Assert.Equal(SampleMessage, Encoding.ASCII.GetString(RawMessageCodec.Serialize(message)));
    }

    [Fact]
    public void MimeBuilder_RebuildsParsableMessageWithStoredHeaders()
    {
        var email = new ArchivedEmail
        {
            MessageId = "rebuilt@example.org",
            Subject = "Rebuilt",
            From = "a@example.org",
            To = "b@example.com",
            Cc = string.Empty,
            Bcc = string.Empty,
            Body = "hello",
            HtmlBody = string.Empty,
            SentDate = new DateTime(2026, 10, 24, 10, 0, 0),
            RawHeaders = "Received: from mx.example.org\r\nX-Spam-Score: 4.2\r\nSubject: ignored\r\n",
            Attachments = new List<EmailAttachment>
            {
                new() { FileName = "a.bin", ContentType = "application/octet-stream", Content = new byte[] { 1, 2, 3 }, Size = 3 }
            }
        };
        var helper = new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" }));

        var bytes = RawMessageCodec.Serialize(ArchivedEmailMimeBuilder.Build(email, helper));
        using var parsed = MimeMessage.Load(new MemoryStream(bytes));

        Assert.Equal("Rebuilt", parsed.Subject);
        Assert.Equal("rebuilt@example.org", parsed.MessageId);
        Assert.Equal("4.2", parsed.Headers["X-Spam-Score"]);
        Assert.Equal("from mx.example.org", parsed.Headers["Received"]);
        var attachment = Assert.Single(parsed.Attachments.OfType<MimePart>());
        Assert.Equal("a.bin", attachment.FileName);
    }
}
