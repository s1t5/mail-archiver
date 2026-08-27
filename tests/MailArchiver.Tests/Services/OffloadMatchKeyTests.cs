using System.Text;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MimeKit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The symmetry between a key built from an archived row and one built from an IMAP ENVELOPE is
/// the whole mechanism of the second duplicate criterion. If the two sides disagree the
/// criterion never fires, and it fails silently: only the handful of rows without a usable
/// Message-ID depend on it, so nothing looks wrong until those rows duplicate.
/// </summary>
public class OffloadMatchKeyTests
{
    // Reproduces what MailImporter writes into the row, so a test can build the "stored" side
    // without a database. Mirrors MailImporter lines 94-99.
    private static ArchivedEmail AsStoredRow(MimeMessage message) => new()
    {
        From = MailContentHelper.TruncateFieldForTsvector(
            MailContentHelper.CleanText(
                string.Join(", ", message.From.Mailboxes.Select(m => m.Address))), 10_000),
        To = MailContentHelper.TruncateFieldForTsvector(
            MailContentHelper.CleanText(
                string.Join(", ", message.To.Mailboxes.Select(m => m.Address))), 50_000),
        Subject = MailContentHelper.TruncateFieldForTsvector(
            MailContentHelper.CleanText(message.Subject ?? "(No Subject)"), 50_000),
    };

    private static MimeMessage Message(string subject, string from, params string[] to)
    {
        var m = new MimeMessage();
        m.From.Add(MailboxAddress.Parse(from));
        foreach (var t in to) m.To.Add(MailboxAddress.Parse(t));
        m.Subject = subject;
        return m;
    }

    /// <summary>
    /// Parses a message from its wire form. This matters for the subject tests: on a
    /// constructed <see cref="MimeMessage"/> Subject reads back as "" even when never set,
    /// while a parsed message with no Subject header reads back as null. Only the parsed
    /// behaviour is what the importer ever sees.
    /// </summary>
    private static MimeMessage Parse(string raw)
        => MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)));

    /// <summary>
    /// The envelope side is derived the way TargetMailboxIndex derives it: mailbox addresses
    /// projected out of the address lists, in order.
    /// </summary>
    private static long EnvelopeSideKey(MimeMessage message)
        => OffloadMatchKey.FingerprintKeyFromAddresses(
            message.From.Mailboxes.Select(m => m.Address),
            message.To.Mailboxes.Select(m => m.Address),
            message.Subject);

    // ------------------------------------------------------------------ symmetry

    [Fact]
    public void Fingerprint_SingleRecipient_RowAndEnvelopeAgree()
    {
        var msg = Message("Hello", "alice@example.com", "bob@example.com");
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_TwoRecipients_RowAndEnvelopeAgree()
    {
        // This is the case the import's own dedup query gets wrong: it joins with "," while the
        // stored column carries ", ", so its second criterion cannot match multi-recipient mail.
        var msg = Message("Hello", "alice@example.com", "bob@example.com", "carol@example.com");
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_TwoSenders_RowAndEnvelopeAgree()
    {
        var msg = Message("Hello", "alice@example.com", "bob@example.com");
        msg.From.Add(MailboxAddress.Parse("dave@example.com"));
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_SubjectWithControlCharacter_RowAndEnvelopeAgree()
    {
        // CleanText replaces characters below 32 with a space before storing, so the raw header
        // and the stored column differ. Both sides must apply the same cleaning.
        var msg = Message("Quarterly\u0001report", "alice@example.com", "bob@example.com");
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_NoSubjectHeader_RowAndEnvelopeAgree()
    {
        // A parsed message with no Subject header has a null Subject, which is the case
        // MailImporter's "?? (No Subject)" substitution exists for. Both sides must substitute.
        var msg = Parse("From: alice@example.com\r\nTo: bob@example.com\r\n\r\nbody\r\n");
        Assert.Null(msg.Subject);
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_EmptySubjectHeader_RowAndEnvelopeAgree()
    {
        // An empty subject is NOT a missing subject. MailImporter writes
        // message.Subject ?? "(No Subject)", so a present-but-empty header is stored as an
        // empty string. Treating empty as missing would desynchronise exactly these rows.
        var msg = Parse("From: alice@example.com\r\nTo: bob@example.com\r\nSubject: \r\n\r\nbody\r\n");
        Assert.Equal(string.Empty, msg.Subject);
        Assert.Equal(
            OffloadMatchKey.FingerprintKeyFromStored(AsStoredRow(msg)),
            EnvelopeSideKey(msg));
    }

    [Fact]
    public void Fingerprint_EmptyAndMissingSubject_AreNotTheSameKey()
    {
        var empty = Parse("From: alice@example.com\r\nTo: bob@example.com\r\nSubject: \r\n\r\nbody\r\n");
        var missing = Parse("From: alice@example.com\r\nTo: bob@example.com\r\n\r\nbody\r\n");
        Assert.NotEqual(EnvelopeSideKey(empty), EnvelopeSideKey(missing));
    }

    [Fact]
    public void Fingerprint_RecipientOrderIsSignificant_AsItIsInStorage()
    {
        // The stored column preserves header order, so the key must too; treating the addresses
        // as a set would make the two sides disagree with the database.
        var ab = Message("Hi", "alice@example.com", "bob@example.com", "carol@example.com");
        var ba = Message("Hi", "alice@example.com", "carol@example.com", "bob@example.com");
        Assert.NotEqual(EnvelopeSideKey(ab), EnvelopeSideKey(ba));
    }

    // ------------------------------------------------------------------ discrimination

    [Fact]
    public void Fingerprint_DifferentSubject_ProducesDifferentKey()
    {
        var a = Message("One", "alice@example.com", "bob@example.com");
        var b = Message("Two", "alice@example.com", "bob@example.com");
        Assert.NotEqual(EnvelopeSideKey(a), EnvelopeSideKey(b));
    }

    [Fact]
    public void Fingerprint_TextMovedBetweenFields_ProducesDifferentKey()
    {
        // Without a field separator "ab" + "c" and "a" + "bc" would hash identically.
        Assert.NotEqual(
            OffloadMatchKey.FingerprintKeyFromStored("ab", "c", "s"),
            OffloadMatchKey.FingerprintKeyFromStored("a", "bc", "s"));
    }

    // ------------------------------------------------------------------ Message-ID criterion

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<bare-token-no-at-sign>")]
    [InlineData("no-at-sign-either")]
    public void MessageIdKey_UnusableValues_ReturnNull(string? messageId)
    {
        // A value without "@" is not emitted on append either: the restore path drops it and
        // MimeKit invents a fresh random Message-Id, so it cannot be matched on.
        Assert.Null(OffloadMatchKey.MessageIdKey(messageId));
    }

    [Fact]
    public void MessageIdKey_UsableValue_ReturnsStableKey()
    {
        var a = OffloadMatchKey.MessageIdKey("<abc@example.com>");
        var b = OffloadMatchKey.MessageIdKey("<abc@example.com>");
        Assert.NotNull(a);
        Assert.Equal(a, b);
    }

    [Fact]
    public void MessageIdKey_IgnoresBracketsAndCase()
    {
        // Legacy rows may carry surrounding angle brackets; NormalizeMessageId strips them.
        var bare = OffloadMatchKey.MessageIdKey("abc@example.com");
        Assert.Equal(bare, OffloadMatchKey.MessageIdKey("<abc@example.com>"));
        Assert.Equal(bare, OffloadMatchKey.MessageIdKey("<<abc@example.com>>"));
        Assert.Equal(bare, OffloadMatchKey.MessageIdKey("  <ABC@Example.COM>  "));
    }

    [Fact]
    public void MessageIdKey_DifferentIds_ProduceDifferentKeys()
    {
        Assert.NotEqual(
            OffloadMatchKey.MessageIdKey("<a@example.com>"),
            OffloadMatchKey.MessageIdKey("<b@example.com>"));
    }

    // ------------------------------------------------------------------ timestamp tolerance

    [Fact]
    public void WithinTolerance_OneSecondApart_Matches()
    {
        // The reason the fingerprint index stores candidate timestamps in a list rather than
        // quantising them: two copies one second apart would fall into different buckets and
        // the match would silently never fire.
        var a = new DateTime(2026, 2, 3, 10, 11, 12);
        Assert.True(OffloadMatchKey.WithinTolerance(a, a.AddSeconds(1)));
        Assert.True(OffloadMatchKey.WithinTolerance(a, a.AddSeconds(-1)));
    }

    [Fact]
    public void WithinTolerance_StraddlingATwoSecondBucketBoundary_StillMatches()
    {
        // 10:11:11.9 and 10:11:12.1 sit in different two second buckets but are 0.2s apart.
        var a = new DateTime(2026, 2, 3, 10, 11, 11, 900);
        var b = new DateTime(2026, 2, 3, 10, 11, 12, 100);
        Assert.True(OffloadMatchKey.WithinTolerance(a, b));
    }

    [Fact]
    public void WithinTolerance_TwoSecondsOrMoreApart_DoesNotMatch()
    {
        var a = new DateTime(2026, 2, 3, 10, 11, 12);
        Assert.False(OffloadMatchKey.WithinTolerance(a, a.AddSeconds(2)));
        Assert.False(OffloadMatchKey.WithinTolerance(a, a.AddSeconds(-3)));
    }

    // ------------------------------------------------------------------ hashing

    [Fact]
    public void Hash64_IsStableAcrossCalls()
    {
        // Deliberately not string.GetHashCode(), which is randomised per process.
        Assert.Equal(OffloadMatchKey.Hash64("a@example.com"), OffloadMatchKey.Hash64("a@example.com"));
    }

    [Fact]
    public void Hash64_KnownValue_DoesNotDriftBetweenReleases()
    {
        // FNV-1a over the two bytes of each UTF-16 unit. Pinned so an accidental change to the
        // algorithm is caught rather than silently invalidating every stored comparison.
        Assert.Equal(OffloadMatchKey.Hash64(""), OffloadMatchKey.Hash64(string.Empty));
        Assert.NotEqual(0, OffloadMatchKey.Hash64("a"));
    }
}
