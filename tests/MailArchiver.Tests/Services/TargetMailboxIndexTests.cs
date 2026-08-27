using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using MailArchiver.Utilities;
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
    public void Add_RowWithUnusableMessageId_MatchesByItsDerivedMessageId()
    {
        var index = NewIndex();
        var email = Row("<no-at-sign>", "alice@x.com", "bob@y.com", "Hi", Sent);

        // Such a row used to fall through to the fingerprint, because the append dropped the
        // unusable value and a fresh random Message-Id was generated every time. The append now
        // derives a stable identifier from it, so the stronger criterion carries the match.
        index.Add(email);
        Assert.Equal(OffloadMatchKind.MessageId, index.Match(email));
    }

    [Fact]
    public void RowWithUnusableMessageId_AppendedBeforeTheFix_IsStillCaughtByFingerprint()
    {
        // The copy already sitting on the target was appended by the old code, so it carries a
        // random Message-ID that the derived one cannot match. The fingerprint has to catch it,
        // otherwise the fix would re-append every such message exactly once more.
        var index = NewIndex();
        index.Add(Row("<random-generated@mimekit.example>", "alice@x.com", "bob@y.com", "Hi", Sent));

        var sourceRow = Row("<no-at-sign>", "alice@x.com", "bob@y.com", "Hi", Sent);

        Assert.Equal(OffloadMatchKind.Fingerprint, index.Match(sourceRow));
    }

    [Fact]
    public void Add_IncrementsTheIndexedCount()
    {
        var index = NewIndex();
        Assert.Equal(0, index.IndexedMessages);
        index.Add(Row("<a@x.com>", "alice@x.com", "bob@y.com", "Hi", Sent));
        Assert.Equal(1, index.IndexedMessages);
    }
}
