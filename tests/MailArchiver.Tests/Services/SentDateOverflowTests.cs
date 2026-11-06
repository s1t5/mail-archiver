using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Regression tests for GitHub issue #600: EML import failing with
/// <c>22008: interval out of range</c>.
/// <para>
/// The duplicate query compares <c>ArchivedEmail.SentDate</c> with the incoming date by
/// subtracting the two timestamps. PostgreSQL's <c>interval</c> type spans fewer years than
/// <c>timestamp</c> does, so a row holding a <c>SentDate</c> far from the incoming value made
/// that subtraction overflow and aborted the import job.
/// </para>
/// <para>
/// These tests run against the real PostgreSQL test database, because the failure is a
/// server-side type-range error that no in-memory provider reproduces. The out-of-range row is
/// inserted with raw SQL: <see cref="DateTime"/> stops at year 9999, so such a value can only
/// have reached the column through a client that does not share that limit.
/// </para>
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class SentDateOverflowTests
{
    private readonly TestDbFixture _fixture;

    public SentDateOverflowTests(TestDbFixture fixture) => _fixture = fixture;

    private const string From = "sender@example.com";
    private const string To = "test@example.com";
    private const string Subject = "overflow subject";

    private static MailAccount NewAccount() => new()
    {
        Name = "sentdate-overflow-" + Guid.NewGuid().ToString("N")[..8],
        EmailAddress = "test@example.com",
        Username = "test@example.com",
        Password = "x",
        Provider = ProviderType.IMPORT,
        ImapServer = "imap.example.com",
        ImapPort = 993,
        IsEnabled = false
    };

    private static ArchivedEmail NewEmail(int accountId, DateTime sentDate) => new()
    {
        MailAccountId = accountId,
        MessageId = "<" + Guid.NewGuid().ToString("N") + "@example.com>",
        Subject = Subject,
        From = From,
        To = To,
        Cc = string.Empty,
        Bcc = string.Empty,
        SentDate = sentDate,
        ReceivedDate = DateTime.UtcNow,
        IsOutgoing = false,
        HasAttachments = false,
        Body = "body",
        HtmlBody = string.Empty,
        FolderName = "INBOX"
    };

    /// <summary>
    /// Inserts a row whose <c>SentDate</c> is year 294276, the documented upper bound of
    /// PostgreSQL's <c>timestamp</c> type and well beyond anything <see cref="DateTime"/> can
    /// express. A year-1 incoming value then sits roughly 294,000 years away, which is past the
    /// point where <c>timestamp - timestamp</c> overflows the <c>interval</c> type.
    /// </summary>
    private static async Task InsertRowWithFarFutureSentDateAsync(MailArchiverDbContext ctx, int accountId)
    {
        var messageId = "<far-" + Guid.NewGuid().ToString("N") + "@example.com>";

        await ctx.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO mail_archiver.""ArchivedEmails""
                (""MailAccountId"", ""MessageId"", ""Subject"", ""From"", ""To"", ""Cc"", ""Bcc"",
                 ""SentDate"", ""ReceivedDate"", ""IsOutgoing"", ""HasAttachments"", ""Body"",
                 ""HtmlBody"", ""FolderName"")
            VALUES
                ({accountId}, {messageId}, {Subject}, {From}, {To},
                 {string.Empty}, {string.Empty}, timestamp '294276-01-01', now(), false, false,
                 {"body"}, {string.Empty}, {"INBOX"})");
    }

    /// <summary>
    /// The subtraction exactly as EF Core renders it for the pre-fix predicate, run as raw SQL.
    /// Used to prove the scenario under test really does overflow, independently of how EF binds
    /// the parameter.
    /// </summary>
    private static async Task<int> UnclampedMatchCountAsync(MailArchiverDbContext ctx, int accountId)
    {
        var sql = @"
            SELECT count(*)::int AS ""Value""
            FROM mail_archiver.""ArchivedEmails"" a
            WHERE a.""MailAccountId"" = {0}
              AND a.""From"" = {1}
              AND a.""To"" = {2}
              AND a.""Subject"" = {3}
              AND abs(date_part('epoch', a.""SentDate"" - timestamp '0001-01-01 00:00:00')) < 2";

        var result = await ctx.Database
            .SqlQueryRaw<int>(sql, accountId, From, To, Subject)
            .ToListAsync();

        return result.Single();
    }

    /// <summary>
    /// The predicate as it now stands in MailImporter and GraphMailArchiver: both sides of the
    /// subtraction restricted to the plausible range.
    /// </summary>
    private static IQueryable<ArchivedEmail> ClampedDuplicateQuery(
        MailArchiverDbContext ctx, int accountId, DateTime checkSentDate)
    {
        var toleranceSeconds = MailMatchKey.TimestampToleranceSeconds;
        var minSentDate = DateTimeHelper.MinimumPlausibleSentDate;
        var maxSentDate = DateTimeHelper.MaximumPlausibleSentDate;

        return ctx.ArchivedEmails
            .Where(e => e.MailAccountId == accountId)
            .Where(e =>
                e.From == From && e.To == To && e.Subject == Subject &&
                Math.Abs(((e.SentDate < minSentDate ? minSentDate :
                            e.SentDate > maxSentDate ? maxSentDate : e.SentDate)
                          - checkSentDate).TotalSeconds) < toleranceSeconds);
    }

    [Fact]
    public async Task UnclampedSubtraction_AgainstFarFutureSentDate_Overflows()
    {
        // Documents the failure the fix addresses. If this ever stops throwing, the clamp has
        // become unnecessary and the reason for it should be re-examined.
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        await InsertRowWithFarFutureSentDateAsync(ctx, account.Id);

        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            async () => await UnclampedMatchCountAsync(ctx, account.Id));

        Assert.Equal("22008", ex.SqlState);
        Assert.Contains("interval out of range", ex.MessageText);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task ClampedQuery_AgainstFarFutureSentDate_ReturnsInsteadOfThrowing()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        await InsertRowWithFarFutureSentDateAsync(ctx, account.Id);

        // The clamped column side lands on 2100-12-31, the incoming value on 2024-06-15, so the
        // difference is a few decades - no match, but crucially no exception either.
        var result = await ClampedDuplicateQuery(ctx, account.Id, new DateTime(2024, 6, 15, 12, 0, 0))
            .ToListAsync();

        Assert.Empty(result);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task ClampedQuery_YearOneSentDate_DoesNotThrow()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        // The row an earlier version wrote for a message whose Date header MimeKit could not
        // represent (it collapses to DateTimeOffset.MinValue, i.e. year 1).
        ctx.ArchivedEmails.Add(NewEmail(account.Id, new DateTime(1, 1, 1, 0, 0, 0)));
        await ctx.SaveChangesAsync();

        var result = await ClampedDuplicateQuery(ctx, account.Id, new DateTime(2024, 6, 15, 12, 0, 0))
            .ToListAsync();

        Assert.Empty(result);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task ClampedQuery_MatchingWithinTolerance_StillFindsDuplicate()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        var stored = new DateTime(2024, 6, 15, 12, 0, 0);
        ctx.ArchivedEmails.Add(NewEmail(account.Id, stored));
        await ctx.SaveChangesAsync();

        // One second apart: inside the two-second window, so the duplicate must still be found.
        var result = await ClampedDuplicateQuery(ctx, account.Id, stored.AddSeconds(1)).ToListAsync();

        Assert.Single(result);

        await scope.RollbackAsync();
    }

    [Fact]
    public async Task ClampedQuery_ClampedIncomingDate_MatchesRowAtLowerBound()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;

        var account = NewAccount();
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();

        // A re-import of a message with an unrepresentable Date header must recognize the row
        // written earlier for the same message, otherwise every such message is duplicated.
        ctx.ArchivedEmails.Add(NewEmail(account.Id, DateTimeHelper.MinimumPlausibleSentDate));
        await ctx.SaveChangesAsync();

        var clampedIncoming = DateTimeHelper.ClampToPlausibleRange(new DateTime(1, 1, 1));

        var result = await ClampedDuplicateQuery(ctx, account.Id, clampedIncoming).ToListAsync();

        Assert.Single(result);

        await scope.RollbackAsync();
    }
}
