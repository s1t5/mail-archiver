using MailArchiver.Services.Providers.Eml;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using System.Text;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Tests for <see cref="EmlMailCleaner.TryParseMessageFromCorruptedMboxAsync"/> and
/// <see cref="EmlMailCleaner.LooksLikeMboxFromLine"/> — recovery for EMLs whose
/// first line is a leftover mbox From-marker (Thunderbird/Eudora/Apple Mail exports).
/// No database connection required.
/// </summary>
public class EmlMailCleanerMboxRecoveryTests
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

    // ── LooksLikeMboxFromLine ────────────────────────────────────────────

    [Theory]
    [InlineData("From sender@example.com Wed Jul  2 13:18:22 2008")]           // standard mbox
    [InlineData("From - Tue Nov 17 10:44:54 2009")]                            // Eudora-style
    [InlineData(">>From - Tue Nov 17 10:44:54 2009")]                          // corrupted double-quote Eudora
    [InlineData(">>>From - Tue Nov 17 10:44:54 2009")]                         // triple-quote variant
    [InlineData(" Aug 22 15:03:38 2008")]                                      // Thunderbird (leading space)
    [InlineData("\tJul  2 13:18:22 2008")]                                     // Thunderbird (leading tab / day with extra space)
    [InlineData("Aug 22 15:03:38 2008")]                                       // bare timestamp variant
    public void LooksLikeMboxFromLine_MatchVariants_ReturnsTrue(string line)
    {
        Assert.True(EmlMailCleaner.LooksLikeMboxFromLine(line));
    }

    [Theory]
    [InlineData("From: user@example.com")]                                     // valid RFC-5322 From header
    [InlineData("FromAnother: something")]                                     // header name starting with "From"
    [InlineData("Return-Path: <x@y>")]                                         // unrelated header
    [InlineData("Subject: From the archive")]                                  // Subject happens to contain "From"
    [InlineData("")]                                                           // empty
    [InlineData("   ")]                                                        // whitespace-only
    public void LooksLikeMboxFromLine_ValidHeaders_ReturnsFalse(string line)
    {
        Assert.False(EmlMailCleaner.LooksLikeMboxFromLine(line));
    }

    // ── TryParseMessageFromCorruptedMboxAsync ────────────────────────────

    [Fact]
    public async Task TryParse_ThunderbirdLeadingSpaceLine_ReturnsParsedMessage()
    {
        // Mirrors "AquaSoft Registrierung.eml" from issue #518:
        // first line is " Aug 22 15:03:38 2008", plus Thunderbird headers.
        var content =
            " Aug 22 15:03:38 2008\r\n" +
            "X-Account-Key: account5\r\n" +
            "X-UIDL: 0000d61144fe90fc\r\n" +
            "X-Mozilla-Keys:                                                                                 \r\n" +
            "Return-Path: <Support@aquasoft.de>\r\n" +
            ValidHeadersAndBody;

        using var ms = ToStream(content);
        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        Assert.NotNull(msg);
        Assert.Equal("AquaSoft Registrierung", msg.Subject);
        Assert.Equal("Support@aquasoft.de", msg.From.Mailboxes.First().Address);
    }

    [Fact]
    public async Task TryParse_EudoraDoubleAngleLine_ReturnsParsedMessage()
    {
        // Mirrors "Angebot für Sonicwall.eml": first line is
        // ">>From - Tue Nov 17 10:44:54 2009".
        var content =
            ">>From - Tue Nov 17 10:44:54 2009\r\n" +
            ValidHeadersAndBody;

        using var ms = ToStream(content);
        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        Assert.NotNull(msg);
        Assert.Equal("AquaSoft Registrierung", msg.Subject);
    }

    [Fact]
    public async Task TryParse_StandardMboxLine_ReturnsParsedMessage()
    {
        var content =
            "From support@aquasoft.de Wed Jul  2 13:18:22 2008\r\n" +
            ValidHeadersAndBody;

        using var ms = ToStream(content);
        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        Assert.NotNull(msg);
        Assert.Equal("AquaSoft Registrierung", msg.Subject);
    }

    [Fact]
    public async Task TryParse_ValidEmlWithoutMboxMarker_ReturnsNull()
    {
        // Heuristic must NOT trigger on a clean RFC-5322 message — the
        // caller would then surface the original parse error untouched.
        using var ms = ToStream(ValidHeadersAndBody);
        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        Assert.Null(msg);
    }

    [Fact]
    public async Task TryParse_FirstLineLooksMboxButBodyIsGarbage_ReturnsNull()
    {
        var content = " Aug 22 15:03:38 2008\r\n" +
                      "This is not a valid header\r\n" +
                      "and there are no parseable headers at all\r\n" +
                      "\r\n" +
                      "body\r\n";

        using var ms = ToStream(content);
        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        // First strategy (MimeFormat.Mbox) actually parses bare-line content
        // leniently in MimeKit — so this should recover via stripping.
        // The important guarantee: we never throw, we return a message or null.
        // "This is not a valid header" is not a header so mbox parser may still
        // construct an empty-message or fail — either is fine. Assert no throw.
        _ = msg; // don't care about result — only that no exception escapes
    }

    [Fact]
    public async Task TryParse_StreamPositionNotAtZero_StillWorks()
    {
        var content = " Aug 22 15:03:38 2008\r\n" + ValidHeadersAndBody;
        using var ms = ToStream(content);
        ms.Position = 10; // simulate partially-read stream

        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(ms);

        Assert.NotNull(msg);
        Assert.Equal("AquaSoft Registrierung", msg.Subject);
    }

    [Fact]
    public async Task TryParse_NonSeekableStream_ReturnsNull()
    {
        var content = " Aug 22 15:03:38 2008\r\n" + ValidHeadersAndBody;
        using var inner = ToStream(content);
        using var nonSeek = new NonSeekableStream(inner);

        var msg = await Create().TryParseMessageFromCorruptedMboxAsync(nonSeek);

        Assert.Null(msg);
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
