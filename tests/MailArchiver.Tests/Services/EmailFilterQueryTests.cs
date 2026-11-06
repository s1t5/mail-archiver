using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for <see cref="EmailFilterQuery"/>. They assert that the
/// deletion filter resolves exactly the same rows as the archive search, that
/// locked rows are counted separately and never returned as deletable, and that
/// the keyset pagination returns every row exactly once.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class EmailFilterQueryTests
{
    private const string Domain = "@filterquery.test";

    private readonly TestDbFixture _fixture;
    public EmailFilterQueryTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx)
    {
        var account = new MailAccount
        {
            Name = $"fq-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}{Domain}",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    private static ArchivedEmail NewEmail(
        int accountId,
        string subject,
        string body = "body",
        string from = "sender@test.local",
        string folder = "INBOX",
        DateTime? sentDate = null,
        bool isOutgoing = false,
        bool isLocked = false) => new()
        {
            MailAccountId = accountId,
            MessageId = $"<{Guid.NewGuid():N}{Domain}>",
            Subject = subject,
            Body = body,
            HtmlBody = string.Empty,
            From = from,
            To = "recipient@test.local",
            Cc = string.Empty,
            Bcc = string.Empty,
            FolderName = folder,
            SentDate = sentDate ?? DateTime.UtcNow.AddDays(-1),
            ReceivedDate = DateTime.UtcNow,
            IsOutgoing = isOutgoing,
            HasAttachments = false,
            IsLocked = isLocked
        };

    /// <summary>
    /// Best-effort cleanup for the non-transactional tests. Those tests never create
    /// locked rows, so the compliance trigger does not block the delete.
    /// </summary>
    private static async Task CleanupAsync(MailArchiverDbContext ctx)
    {
        var accountIds = await ctx.MailAccounts.AsNoTracking()
            .Where(a => a.EmailAddress.EndsWith(Domain))
            .Select(a => a.Id)
            .ToListAsync();

        if (accountIds.Count == 0) return;

        var emails = await ctx.ArchivedEmails
            .Where(e => accountIds.Contains(e.MailAccountId))
            .ToListAsync();
        ctx.ArchivedEmails.RemoveRange(emails);

        var accounts = await ctx.MailAccounts
            .Where(a => accountIds.Contains(a.Id))
            .ToListAsync();
        ctx.MailAccounts.RemoveRange(accounts);

        await ctx.SaveChangesAsync();
    }

    private static async Task AssertMatchesSearchAsync(
        MailArchiverDbContext ctx,
        EmailDeletionFilter filter,
        string? searchTerm,
        DateTime? fromDate,
        DateTime? toDate,
        int? accountId,
        string? folderName,
        bool? isOutgoing,
        List<int>? allowedAccountIds,
        int expected)
    {
        var (deletable, _, _) = await EmailFilterQuery.CountAsync(ctx, filter, default);
        var ids = await EmailFilterQuery.IdsBatchAsync(ctx, filter, 0, 1000, default);

        var svc = ServiceFactory.CreateEmailCoreService(ctx);
        var (emails, total) = await svc.SearchEmailsAsync(
            searchTerm, fromDate, toDate, accountId, folderName, isOutgoing, 0, 1000, allowedAccountIds);

        Assert.Equal(expected, total);
        Assert.Equal(total, deletable);
        Assert.Equal(emails.Select(e => e.Id).OrderBy(i => i), ids.OrderBy(i => i));
    }

    [Fact]
    public async Task CountAndIds_MatchSearch_ForTermAndAccount()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                NewEmail(acct.Id, "invoice january", body: "your invoice is attached"),
                NewEmail(acct.Id, "invoice february", body: "your invoice is attached"),
                NewEmail(acct.Id, "newsletter", body: "weekly promo"));
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter { SearchTerm = "invoice", AccountId = acct.Id };
            await AssertMatchesSearchAsync(ctx, filter, "invoice", null, null, acct.Id, null, null, null, 2);

            var (_, _, maxId) = await EmailFilterQuery.CountAsync(ctx, filter, default);
            var ids = await EmailFilterQuery.IdsBatchAsync(ctx, filter, 0, 100, default);
            Assert.Equal(ids.Max(), maxId);
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountAndIds_MatchSearch_ForFolderSubtree()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                NewEmail(acct.Id, "root of 2020", folder: "INBOX/2020"),
                NewEmail(acct.Id, "child of 2020", folder: "INBOX/2020/sub"),
                NewEmail(acct.Id, "2021", folder: "INBOX/2021"));
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter { FolderName = "INBOX/2020", AccountId = acct.Id };
            await AssertMatchesSearchAsync(ctx, filter, null, null, null, acct.Id, "INBOX/2020", null, null, 2);
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountAndIds_MatchSearch_ForDateRangeAndDirection()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                NewEmail(acct.Id, "january incoming", sentDate: new DateTime(2020, 1, 15), isOutgoing: false),
                NewEmail(acct.Id, "june outgoing", sentDate: new DateTime(2020, 6, 15), isOutgoing: true),
                NewEmail(acct.Id, "next year", sentDate: new DateTime(2021, 1, 15), isOutgoing: false));
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter
            {
                AccountId = acct.Id,
                FromDate = new DateTime(2020, 1, 1),
                ToDate = new DateTime(2020, 12, 31),
                IsOutgoing = false
            };
            await AssertMatchesSearchAsync(ctx, filter, null,
                new DateTime(2020, 1, 1), new DateTime(2020, 12, 31),
                acct.Id, null, false, null, 1);
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountAndIds_RespectAllowedAccountIds()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var allowed = await SeedAccountAsync(ctx);
            var foreign = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                NewEmail(allowed.Id, "allowed"),
                NewEmail(foreign.Id, "foreign"));
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter { AllowedAccountIds = new List<int> { allowed.Id } };
            await AssertMatchesSearchAsync(ctx, filter, null, null, null, null, null, null,
                new List<int> { allowed.Id }, 1);

            // Explicitly asking for the foreign account must not widen the result.
            var denied = new EmailDeletionFilter
            {
                AccountId = foreign.Id,
                AllowedAccountIds = new List<int> { allowed.Id }
            };
            var (deletable, _, _) = await EmailFilterQuery.CountAsync(ctx, denied, default);
            Assert.Equal(0, deletable);
            Assert.Empty(await EmailFilterQuery.IdsBatchAsync(ctx, denied, 0, 100, default));
        }
        finally
        {
            await CleanupAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Count_ReportsLockedSeparately_AndIdsSkipThem()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var unlocked = NewEmail(acct.Id, "delete me");
            var locked = NewEmail(acct.Id, "keep me", isLocked: true);
            ctx.ArchivedEmails.AddRange(unlocked, locked);
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter { AccountId = acct.Id };
            var (deletable, lockedCount, maxId) = await EmailFilterQuery.CountAsync(ctx, filter, default);
            var ids = await EmailFilterQuery.IdsBatchAsync(ctx, filter, 0, 100, default);

            Assert.Equal(1, deletable);
            Assert.Equal(1, lockedCount);
            Assert.Equal(unlocked.Id, maxId);
            Assert.Equal(new[] { unlocked.Id }, ids);
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }

    [Fact]
    public async Task IdsBatch_PaginatesWithoutDuplicates()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var emails = Enumerable.Range(0, 5)
                .Select(i => NewEmail(acct.Id, $"mail {i}"))
                .ToList();
            ctx.ArchivedEmails.AddRange(emails);
            await ctx.SaveChangesAsync();

            var filter = new EmailDeletionFilter { AccountId = acct.Id };
            var collected = new List<int>();
            var after = 0;
            while (true)
            {
                var batch = await EmailFilterQuery.IdsBatchAsync(ctx, filter, after, 2, default);
                if (batch.Count == 0) break;
                collected.AddRange(batch);
                after = batch[^1];
            }

            Assert.Equal(5, collected.Count);
            Assert.Equal(5, collected.Distinct().Count());
            Assert.Equal(collected.OrderBy(i => i), collected);
            Assert.Equal(emails.Select(e => e.Id).OrderBy(i => i), collected);
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }

    [Fact]
    public async Task IdsBatch_RespectsUpperBound()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.AddRange(
                NewEmail(acct.Id, "first"),
                NewEmail(acct.Id, "second"),
                NewEmail(acct.Id, "third"));
            await ctx.SaveChangesAsync();

            var ordered = await ctx.ArchivedEmails.AsNoTracking()
                .Where(e => e.MailAccountId == acct.Id)
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .ToListAsync();

            var filter = new EmailDeletionFilter { AccountId = acct.Id, UpperBoundId = ordered[1] };
            var ids = await EmailFilterQuery.IdsBatchAsync(ctx, filter, 0, 100, default);

            Assert.Equal(ordered.Take(2), ids);
        }
        finally
        {
            await scope.RollbackAsync();
        }
    }
}
