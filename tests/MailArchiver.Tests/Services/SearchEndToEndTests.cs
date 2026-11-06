using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MailArchiver.Utilities;
using MailArchiver.Auth.Options;
using Npgsql;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// End-to-end tests for the advanced search syntax against the PostgreSQL test database.
/// Runs the real raw-SQL search path (<see cref="EmailCoreService.SearchEmailsOptimizedAsync"/>)
/// and asserts the exact set of matching mails. Each test seeds its own account with six
/// mails (committed, because the search opens its own connection) and removes it afterwards;
/// every search is scoped to that account so other rows in the test database do not interfere.
/// Mails are referred to by their seed number 1..6 (encoded in the MessageId).
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class SearchEndToEndTests : IAsyncLifetime
{
    private readonly TestDbFixture _fixture;
    private readonly string _run = Guid.NewGuid().ToString("N");
    private int _accountId;

    public SearchEndToEndTests(TestDbFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var ctx = _fixture.CreateContext();
        var account = new MailAccount
        {
            Name = $"search-{_run}".Substring(0, 25),
            EmailAddress = $"{_run}@test.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        _accountId = account.Id;

        var mails = new[]
        {
            Mail(1, "WD Red Pro 8 TB Festplatte", "Rechnung fuer die Festplatte", "shop@cyberport.de", "2025-01-06"),
            Mail(2, "Newsletter Angebote", "auto und fahrrad im angebot", "news@shop.de", "2025-03-01"),
            Mail(3, "Zahlungserinnerung", "offene rechnung mahnung bitte zahlen", "billing@x.de", "2025-02-01"),
            Mail(4, "Quartalsabrechnung", "die abrechnung liegt bei", "buchhaltung@x.de", "2025-04-01"),
            Mail(5, "Fahrrad Tour", "wir fahren mit dem fahrrad", "club@x.de", "2025-05-01"),
            Mail(6, "R&D O'Reilly Q&A", "budget planning notes", "team@x.de", "2025-06-01"),
        };
        ctx.ArchivedEmails.AddRange(mails);
        await ctx.SaveChangesAsync();

        // Mail 1 & 4 have a REAL file attachment (ContentId NULL); note HasAttachments=false on
        // mail 1, proving has:attachment does not depend on the flag. Mail 5 has ONLY an inline
        // signature image (cid ContentId) and must NOT count as "has attachment".
        ctx.EmailAttachments.AddRange(
            Attachment(mails[0].Id, "rechnung.pdf", "application/pdf", null),
            Attachment(mails[3].Id, "abrechnung.pdf", "application/pdf", null),
            Attachment(mails[4].Id, "inline_logo", "image/png", "logo@signatur"));
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = _fixture.CreateContext();
        await ctx.EmailAttachments.Where(a => a.ArchivedEmail.MailAccountId == _accountId).ExecuteDeleteAsync();
        await ctx.ArchivedEmails.Where(e => e.MailAccountId == _accountId).ExecuteDeleteAsync();
        await ctx.MailAccounts.Where(a => a.Id == _accountId).ExecuteDeleteAsync();
    }

    private ArchivedEmail Mail(int n, string subject, string body, string from, string date) => new()
    {
        MailAccountId = _accountId,
        MessageId = $"{_run}-m{n}",
        Subject = subject,
        Body = body,
        HtmlBody = string.Empty,
        From = from,
        To = "me@x.de",
        Cc = string.Empty,
        Bcc = string.Empty,
        SentDate = DateTime.Parse(date),
        ReceivedDate = DateTime.Parse(date),
        IsOutgoing = false,
        HasAttachments = false,
        FolderName = "INBOX"
    };

    private static EmailAttachment Attachment(int emailId, string name, string type, string? contentId) => new()
    {
        ArchivedEmailId = emailId,
        FileName = name,
        ContentType = type,
        ContentId = contentId,
        Size = 1234,
        Content = new byte[] { 1, 2, 3 }
    };

    private EmailCoreService Svc() => new(
        _fixture.CreateContext(),
        NullLogger<EmailCoreService>.Instance,
        new DateTimeHelper(Options.Create(new TimeZoneOptions { DisplayTimeZoneId = "Europe/Berlin" })),
        Options.Create(new BatchOperationOptions()),
        configuration: new ConfigurationBuilder().Build());

    private async Task<HashSet<int>> Ids(string term)
    {
        var (emails, total) = await Svc().SearchEmailsOptimizedAsync(term, null, null, _accountId, null!, null, 0, 100);
        Assert.Equal(total, emails.Count);
        return emails.Select(e => int.Parse(e.MessageId[(e.MessageId.LastIndexOf("-m") + 2)..])).ToHashSet();
    }

    [Fact] public async Task And_returns_only_docs_with_all_words()
        => Assert.Equal(new HashSet<int> { 3 }, await Ids("rechnung mahnung"));

    [Fact] public async Task Single_word_prefix_not_midword()
        => Assert.Equal(new HashSet<int> { 1, 3 }, await Ids("rechnung"));

    [Fact] public async Task Exclude_removes_matches()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("rechnung -mahnung"));

    // Pure exclusion ("exclude only") takes the De Morgan path: NOT a AND NOT b is evaluated as
    // an anti-membership filter against the positive set (a OR b). Mail 4 ("abrechnung") stays:
    // words are prefix-matched, never mid-word.
    [Fact] public async Task Pure_negation_of_two_words_excludes_both()
        => Assert.Equal(new HashSet<int> { 2, 4, 5, 6 }, await Ids("-rechnung -mahnung"));

    [Fact] public async Task Pure_negation_or_group_excludes_only_mails_with_both_words()
        => Assert.Equal(new HashSet<int> { 1, 3, 4, 5, 6 }, await Ids("-auto OR -fahrrad"));

    [Fact] public async Task Or_returns_union()
        => Assert.Equal(new HashSet<int> { 2, 5 }, await Ids("auto OR fahrrad"));

    [Fact] public async Task Substring_matches_midword()
        => Assert.Equal(new HashSet<int> { 1, 3, 4 }, await Ids("*rechn*"));

    [Fact] public async Task Field_subject()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("subject:festplatte"));

    [Fact] public async Task Field_from()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("from:cyberport"));

    [Fact] public async Task Short_token_finds()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("wd"));

    [Fact] public async Task Or_across_fields()
        => Assert.Equal(new HashSet<int> { 1, 3 }, await Ids("from:cyberport OR from:billing"));

    [Fact] public async Task Or_across_substrings()
        => Assert.Equal(new HashSet<int> { 1, 2, 5 }, await Ids("*cyber* OR *fahrr*"));

    [Fact] public async Task Phrase_matches_exact()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("\"WD Red\""));

    // has:attachment must match only REAL file attachments (ContentId IS NULL),
    // independent of the ArchivedEmails.HasAttachments flag.
    [Fact] public async Task Has_attachment_matches_only_real_files()
        => Assert.Equal(new HashSet<int> { 1, 4 }, await Ids("has:attachment"));

    // A mail whose only attachment is an inline signature image (cid ContentId) must NOT match.
    [Fact] public async Task Has_attachment_ignores_inline_images()
        => Assert.DoesNotContain(5, await Ids("has:attachment"));

    [Fact] public async Task Negated_has_attachment_returns_complement()
        => Assert.Equal(new HashSet<int> { 2, 3, 5, 6 }, await Ids("-has:attachment"));

    [Fact] public async Task Word_and_has_attachment_intersect()
        => Assert.Equal(new HashSet<int> { 1 }, await Ids("rechnung has:attachment"));

    // Quoted field values keep syntax chars (subject:"R&D" must match "R&D").
    [Fact] public async Task Field_quoted_value_with_ampersand()
        => Assert.Equal(new HashSet<int> { 6 }, await Ids("subject:\"R&D\""));

    // Quoted field value with an apostrophe matches literally.
    [Fact] public async Task Field_quoted_value_with_apostrophe()
        => Assert.Equal(new HashSet<int> { 6 }, await Ids("subject:\"O'Reilly\""));

    // A plain word with an apostrophe still matches via FTS (O'Reilly -> lexeme oreilly).
    [Fact] public async Task Word_with_apostrophe_matches()
        => Assert.Equal(new HashSet<int> { 6 }, await Ids("O'Reilly"));

    // A punctuated phrase prefilters via the split sub-lexemes (R&D -> r<->d), so the positive
    // phrase matches mail 6 ...
    [Fact] public async Task Phrase_with_ampersand_matches()
        => Assert.Equal(new HashSet<int> { 6 }, await Ids("\"R&D\""));

    // ... and the negated phrase excludes exactly that mail.
    [Fact] public async Task Negated_phrase_with_ampersand_excludes_match()
        => Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5 }, await Ids("-\"R&D\""));

    [Fact] public async Task Phrase_with_apostrophe_matches()
        => Assert.Equal(new HashSet<int> { 6 }, await Ids("\"O'Reilly\""));

    // When OR distribution exceeds the CNF bound, bounding is a sound superset (drops
    // AND-constraints only), so a mail satisfying one branch is still found.
    [Fact]
    public async Task Or_distribution_over_bound_preserves_recall()
    {
        var many = string.Join(" ", Enumerable.Range(1, 300).Select(i => "zq" + i));
        Assert.Contains(1, await Ids($"subject:(festplatte) OR body:({many})"));
    }

    // The startup ensure in Program.cs must create the trigram GIN index when pg_trgm is
    // available but the migration skipped it. Drops the index, runs the ensure SQL and
    // asserts it reappears. Skipped when pg_trgm is not installed on the test server.
    [Fact]
    public async Task TrgmIndex_startup_ensure_creates_index_when_extension_available()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        try
        {
            await new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS pg_trgm", conn).ExecuteNonQueryAsync();
        }
        catch (PostgresException)
        {
            return;
        }

        await new NpgsqlCommand("DROP INDEX IF EXISTS mail_archiver.\"idx_archivedemails_trgm_search\"", conn)
            .ExecuteNonQueryAsync();

        await new NpgsqlCommand(TrgmSearchIndex.EnsureSql, conn).ExecuteNonQueryAsync();

        var count = (int)(await new NpgsqlCommand(@"SELECT COUNT(*)::int FROM pg_indexes
                              WHERE schemaname = 'mail_archiver'
                                AND indexname  = 'idx_archivedemails_trgm_search'", conn)
            .ExecuteScalarAsync() ?? 0);
        Assert.Equal(1, count);
    }
}
