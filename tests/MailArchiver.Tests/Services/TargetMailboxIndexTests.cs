using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Utilities;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The IMAP fetch that normally fills the index cannot be unit tested, but the matching on top
/// of it can, against a hand-built index. Criterion precedence and the send-time tolerance are
/// what decide whether a repeated offload duplicates mail.
/// </summary>
public class TargetMailboxIndexTests
{
    private static TargetMailboxIndex NewIndex(string timeZoneId = "Etc/UCT")
        => new(new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = timeZoneId })));

    private static readonly DateTimeHelper TestDateTimeHelper =
        new(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Etc/UCT" }));

    private static ArchivedEmail Row(string? messageId, string from, string to, string subject, DateTime sent)
        => new() { MessageId = messageId ?? string.Empty, From = from, To = to, Subject = subject, SentDate = sent };

    private static readonly DateTime Sent = new(2026, 2, 3, 10, 11, 12);

    [Fact]
    public void Match_EmptyIndex_FindsNothing()
    {
        var index = NewIndex();
        Assert.Equal(OffloadMatchKind.None,
            index.Match(Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Match_ByMessageId()
    {
        var index = NewIndex();
        index.AddRaw("<a@x.com>", "someone@else.com", "nobody@else.com", "Different", Sent.AddYears(-3));

        // Message-ID alone is enough; none of the other fields agree.
        Assert.Equal(OffloadMatchKind.MessageId,
            index.Match(Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Match_MessageIdTakesPrecedenceOverFingerprint()
    {
        var index = NewIndex();
        index.AddRaw("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent);

        // Both criteria would match; the reported kind must be the more reliable one, because
        // MatchedByFingerprint is a counter the operator reads.
        Assert.Equal(OffloadMatchKind.MessageId,
            index.Match(Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Match_ByFingerprint_WhenMessageIdIsUnusable()
    {
        var index = NewIndex();
        index.AddRaw("<indexed@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent);

        // A stored Message-ID without "@" cannot be matched on, so the fingerprint has to carry
        // this row. These are exactly the rows that would otherwise duplicate on every run.
        Assert.Equal(OffloadMatchKind.Fingerprint,
            index.Match(Row("<no-at-sign>", "alice@x.com", "bob@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Match_ByFingerprint_WhenMessageIdIsMissingEntirely()
    {
        var index = NewIndex();
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Hi", Sent);
        Assert.Equal(OffloadMatchKind.Fingerprint,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Match_Fingerprint_OneSecondApart_StillMatches()
    {
        var index = NewIndex();
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Hi", Sent);

        Assert.Equal(OffloadMatchKind.Fingerprint,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Hi", Sent.AddSeconds(1))));
    }

    [Fact]
    public void Match_Fingerprint_ThreeSecondsApart_DoesNotMatch()
    {
        var index = NewIndex();
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Hi", Sent);

        Assert.Equal(OffloadMatchKind.None,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Hi", Sent.AddSeconds(3))));
    }

    [Fact]
    public void Match_Fingerprint_SameFieldsDifferentDay_DoesNotMatch()
    {
        var index = NewIndex();
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Weekly report", Sent);

        // A recurring message with an identical subject a week later is a different message.
        Assert.Equal(OffloadMatchKind.None,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Weekly report", Sent.AddDays(7))));
    }

    [Fact]
    public void Match_Fingerprint_SeveralCandidatesUnderOneKey_ChecksThemAll()
    {
        var index = NewIndex();
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Weekly report", Sent);
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Weekly report", Sent.AddDays(7));
        index.AddRaw(null, "alice@x.com", "bob@y.com", "Weekly report", Sent.AddDays(14));

        // The middle candidate must be found, not just the first stored under the key.
        Assert.Equal(OffloadMatchKind.Fingerprint,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Weekly report", Sent.AddDays(7))));
        Assert.Equal(OffloadMatchKind.None,
            index.Match(Row(null, "alice@x.com", "bob@y.com", "Weekly report", Sent.AddDays(21))));
    }

    [Fact]
    public void Match_Fingerprint_MultiRecipient_MatchesOnStorageFormat()
    {
        var index = NewIndex();
        // Stored columns join with ", ". A key built the way the import's dedup query builds it
        // would use "," and would never match this row.
        index.AddRaw(null, "alice@x.com", "bob@y.com, carol@y.com", "Hi", Sent);

        Assert.Equal(OffloadMatchKind.Fingerprint,
            index.Match(Row(null, "alice@x.com", "bob@y.com, carol@y.com", "Hi", Sent)));
    }

    [Fact]
    public void Add_MakesASubsequentMatchSucceed()
    {
        var index = NewIndex();
        var email = Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent);

        Assert.Equal(OffloadMatchKind.None, index.Match(email));

        // Called after every successful append, so duplicates inside one run are caught too and
        // not only duplicates against an earlier run.
        index.Add(email);
        Assert.Equal(OffloadMatchKind.MessageId, index.Match(email));
    }

    [Fact]
    public void Add_RowWithUnusableMessageId_IsStillMatchableByFingerprint()
    {
        var index = NewIndex();
        var email = Row("<no-at-sign>", "alice@x.com", "bob@y.com", "Hi", Sent);

        index.Add(email);
        Assert.Equal(OffloadMatchKind.Fingerprint, index.Match(email));
    }

    [Fact]
    public void Add_IncrementsTheIndexedCount()
    {
        var index = NewIndex();
        Assert.Equal(0, index.IndexedMessages);
        index.Add(Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent));
        Assert.Equal(1, index.IndexedMessages);
    }

    [Fact]
    public async Task BuildAsync_UsesInjectedUnionEnumerator_NotPlainRecursiveList()
    {
        var spyCalls = 0;
        Func<ImapClient, Task<List<IMailFolder>>> spy = _ =>
        {
            spyCalls++;
            return Task.FromResult(new List<IMailFolder>());
        };

        // BuildAsync can run without an open connection when the enumerator itself
        // yields no folders (early return, no IMAP operation).
        var index = await TargetMailboxIndex.BuildAsync(
            client: null!,
            dateTimeHelper: TestDateTimeHelper,
            logger: NullLogger.Instance,
            prefetchMaxMessages: 10,
            folderEnumerator: spy,
            restrictToFolder: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, spyCalls);
        Assert.Equal(0, index.IndexedFolders);
        Assert.False(index.ScopeReduced); // an empty list is not a cap or error case
    }

    [Fact]
    public void ToStoredTimestamp_NullEnvelopeDate_FallsBackToInternalDate()
    {
        // H2: without a Date header in the ENVELOPE the index timestamp must not
        // collapse to DateTime.MinValue but use INTERNALDATE instead.
        var index = NewIndex();

        var internalDate = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(2));
        var result = index.TimestampForEnvelope(null, internalDate);

        // 10:00 at +02:00 is 08:00 UCT, and the display timezone here is Etc/UCT —
        // the same conversion ToStoredTimestamp always applies.
        Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 0), result);
        Assert.NotEqual(DateTime.MinValue, result);
    }

    [Fact]
    public void ToStoredTimestamp_WithEnvelopeDate_IgnoresInternalDate()
    {
        var index = NewIndex();

        var envelopeDate = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(2));
        var internalDate = new DateTimeOffset(2026, 6, 15, 18, 0, 0, TimeSpan.FromHours(2));
        var result = index.TimestampForEnvelope(envelopeDate, internalDate);

        // The envelope date wins over INTERNALDATE; 10:00 at +02:00 is 08:00 UCT.
        Assert.Equal(new DateTime(2026, 6, 15, 8, 0, 0), result);
    }

    [Fact]
    public void AddRaw_ThenMatch_RowWithoutMessageIdButStoredSentDate_MatchesByFingerprint()
    {
        // Row as the archiver writes it when the Date header is missing: SentDate from the
        // Received fallback chain, no Message-ID. The index entry comes from an ENVELOPE
        // without a Date (formerly DateTime.MinValue → never matched).
        var index = NewIndex();
        var storedSentDate = new DateTime(2026, 6, 15, 12, 0, 0);

        index.AddRaw(messageId: null, from: "a@b.c", to: "d@e.f", subject: "s", sentDate: storedSentDate);

        var stored = new ArchivedEmail
        {
            MessageId = null, From = "a@b.c", To = "d@e.f", Subject = "s",
            SentDate = storedSentDate,
        };

        Assert.Equal(OffloadMatchKind.Fingerprint, index.Match(stored));
    }
}
