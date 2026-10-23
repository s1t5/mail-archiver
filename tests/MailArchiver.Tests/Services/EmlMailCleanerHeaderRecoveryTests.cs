using MailArchiver.Services.Providers.Eml;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Tests for the tolerant header recovery (<see cref="EmlMailCleaner.TryRecoverHeadersAsync"/>)
/// The mbox From-line cases live in <see cref="EmlMailCleanerMboxRecoveryTests"/>.
/// No database connection required.
/// </summary>
public class EmlMailCleanerHeaderRecoveryTests
{
    private static EmlMailCleaner Create() =>
        new(NullLogger<EmlMailCleaner>.Instance);

    private static MemoryStream ToStream(string content) =>
        new(Encoding.Latin1.GetBytes(content));

    private const string ValidHeadersAndBody =
        "From: Support <Support@aquasoft.de>\r\n" +
        "To: user@example.com\r\n" +
        "Subject: AquaSoft Registrierung\r\n" +
        "Date: Wed, 02 Jul 2008 11:10:38 +0200\r\n" +
        "Message-Id: <7.0.0.16.2.20080702105947.03a1b8b0@aquasoft.de>\r\n" +
        "Mime-Version: 1.0\r\n" +
        "Content-Type: text/plain; charset=\"iso-8859-1\"\r\n" +
        "\r\n" +
        "Hallo Welt\r\n";


    [Fact]
    public async Task TryRecover_MailStoreBannerLine_ReturnsParsedMessage()
    {
        var content =
            "Microsoft Mail Internet Headers Version 2.0\r\n" +
            "Received: from mail.internal.example.com ([192.168.0.3]) by mail.internal.example.com with Microsoft SMTPSVC(6.0.3790.1830);\r\n" +
            "\t Mon, 3 Jul 2006 15:21:51 +0930\r\n" +
            "From: \"Sender Name\" <sender@example.com>\r\n" +
            "To: <recipient@example.com>\r\n" +
            "Subject: example subject\r\n" +
            "Date: Mon, 3 Jul 2006 15:21:18 +0930\r\n" +
            "Message-ID: <000001c69e64$b67d6ff0$0301010a@example>\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: text/plain\r\n" +
            "\r\n" +
            "body\r\n";

        using var ms = ToStream(content);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.NotNull(result.Message);
        Assert.Equal(HeaderRecoveryCategory.BannerLine, result.Category);
        Assert.Equal("example subject", result.Message.Subject);
        Assert.Equal("sender@example.com", result.Message.From.Mailboxes.First().Address);
    }

    // ── Junk lines before the first real header ──────────────────────────

    [Fact]
    public async Task TryRecover_JunkLinesBeforeHeaders_ReturnsParsedMessage()
    {
        var content =
            "some random junk line\r\n" +
            "not a valid header at all\r\n" +
            ValidHeadersAndBody;

        using var ms = ToStream(content);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.NotNull(result.Message);
        Assert.Equal(HeaderRecoveryCategory.JunkBeforeHeaders, result.Category);
        Assert.Equal("AquaSoft Registrierung", result.Message.Subject);
        Assert.Equal("Support@aquasoft.de", result.Message.From.Mailboxes.First().Address);
    }

    // ── Blank line splitting the header block (#499 pattern) ─────────────

    [Fact]
    public async Task TryRecover_BlankLineSplittingHeaderBlock_ReturnsParsedMessage()
    {
        var content =
            "From: Support <Support@aquasoft.de>\r\n" +
            "\r\n" +
            "Subject: AquaSoft Registrierung\r\n" +
            "Date: Wed, 02 Jul 2008 11:10:38 +0200\r\n" +
            "\r\n" +
            "Hallo Welt\r\n";

        using var ms = ToStream(content);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.NotNull(result.Message);
        Assert.Equal(HeaderRecoveryCategory.SplitHeaderBlock, result.Category);
        Assert.Equal("AquaSoft Registrierung", result.Message.Subject);
        Assert.Equal("Support@aquasoft.de", result.Message.From.Mailboxes.First().Address);
    }

    // ── Body content survives the rebuild ────────────────────────────────

    [Fact]
    public async Task TryRecover_BodyContentIsPreserved()
    {
        var content =
            "Microsoft Mail Internet Headers Version 2.0\r\n" +
            "From: a@b.com\r\n" +
            "Subject: body check\r\n" +
            "Content-Type: text/plain\r\n" +
            "\r\n" +
            "The actual body text\r\n" +
            "second body line\r\n";

        using var ms = ToStream(content);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.NotNull(result.Message);
        var text = Assert.IsType<TextPart>(result.Message.BodyParts.OfType<TextPart>().First());
        Assert.Contains("The actual body text", text.Text);
        Assert.Contains("second body line", text.Text);
    }

    // ── Unrecoverable content must not throw ─────────────────────────────

    [Fact]
    public async Task TryRecover_OnlyGarbage_ReturnsNullAndUnrecoverable()
    {
        var content = "garbage line one\r\n" +
                      "another garbage line\r\n" +
                      "yet more garbage\r\n" +
                      "\r\n" +
                      "plain body\r\n";

        using var ms = ToStream(content);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.Null(result.Message);
        Assert.Equal(HeaderRecoveryCategory.Unrecoverable, result.Category);
    }

    // ── Clean EML: recovery must not be applicable ───────────────────────

    [Fact]
    public async Task TryRecover_CleanEml_ReturnsNullBecauseHeuristicNotTriggered()
    {
        // A clean message should never be handed to recovery by callers, and the
        // mbox heuristic must not treat it as an mbox artifact.
        using var ms = ToStream(ValidHeadersAndBody);
        var cleaner = Create();
        var recovered = await cleaner.TryParseMessageFromCorruptedMboxAsync(ms);

        // Legacy wrapper: null means "no mbox artifact found" — caller keeps the
        // original parse error. The generic rebuild would parse it fine, but the
        // wrapper semantics must stay backwards compatible for existing callers.
        Assert.Null(recovered);
    }

    [Fact]
    public async Task TryRecover_CleanEmlViaRecovery_ParsesUnchanged()
    {
        // Direct recovery call on a clean message still yields the same message.
        using var ms = ToStream(ValidHeadersAndBody);
        var result = await Create().TryRecoverHeadersAsync(ms);

        Assert.NotNull(result.Message);
        Assert.Equal("AquaSoft Registrierung", result.Message.Subject);
    }

    // ── Stream position is preserved for the caller ──────────────────────

    [Fact]
    public async Task TryRecover_ResetsStreamPositionToZero()
    {
        var content = "Microsoft Mail Internet Headers Version 2.0\r\n" + ValidHeadersAndBody;
        using var ms = ToStream(content);

        await Create().TryRecoverHeadersAsync(ms);

        Assert.Equal(0, ms.Position);
    }

    // ── Non-seekable streams ─────────────────────────────────────────────

    [Fact]
    public async Task TryRecover_NonSeekableStream_ReturnsUnrecoverable()
    {
        var content = "Microsoft Mail Internet Headers Version 2.0\r\n" + ValidHeadersAndBody;
        using var inner = ToStream(content);
        using var nonSeek = new NonSeekableStream(inner);

        var result = await Create().TryRecoverHeadersAsync(nonSeek);

        Assert.Null(result.Message);
        Assert.Equal(HeaderRecoveryCategory.Unrecoverable, result.Category);
    }

    /// <summary>Stream wrapper that reports CanSeek = false.</summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;
        public NonSeekableStream(Stream inner) => _inner = inner;
        public override bool CanSeek => false;
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}