using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services;
using MailArchiver.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Integration tests for the non-static parts of <see cref="AccountStorageService"/>
/// against the PostgreSQL Dev database. Each test runs inside a rolled-back transaction.
/// </summary>
[Collection(TestDbFixture.CollectionName)]
public class AccountStorageServiceTests
{
    private readonly TestDbFixture _fixture;
    public AccountStorageServiceTests(TestDbFixture fixture) => _fixture = fixture;

    private static async Task<MailAccount> SeedAccountAsync(MailArchiverDbContext ctx)
    {
        var account = new MailAccount
        {
            Name = $"st-{Guid.NewGuid():N}".Substring(0, 25),
            EmailAddress = $"{Guid.NewGuid():N}@test.local",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = DateTime.UtcNow
        };
        ctx.MailAccounts.Add(account);
        await ctx.SaveChangesAsync();
        return account;
    }

    // ============================================================
    // GetStorageForAccountsAsync
    // ============================================================

    [Fact]
    public async Task GetStorageForAccountsAsync_EmptyList_ReturnsEmpty()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            var result = await svc.GetStorageForAccountsAsync(Enumerable.Empty<int>());
            Assert.Empty(result);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetStorageForAccountsAsync_NoCache_ReturnsZeroPerAccount()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            var result = await svc.GetStorageForAccountsAsync(new[] { acct.Id });
            Assert.Equal(AccountStorageService.FormatFileSize(0), result[acct.Id]);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetStorageForAccountsAsync_CacheHit_ReturnsFormatted()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.AccountStorageCaches.Add(new AccountStorageCache
            {
                MailAccountId = acct.Id,
                MailBytes = 1024,
                AttachmentBytes = 512,
                TotalBytes = 1536,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            var result = await svc.GetStorageForAccountsAsync(new[] { acct.Id });
            Assert.Equal(AccountStorageService.FormatFileSize(1536), result[acct.Id]);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task GetStorageForAccountsAsync_MixedAccounts_ReturnsEach()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            ctx.AccountStorageCaches.Add(new AccountStorageCache
            {
                MailAccountId = a1.Id, MailBytes = 0, AttachmentBytes = 0, TotalBytes = 2048,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            var result = await svc.GetStorageForAccountsAsync(new[] { a1.Id, a2.Id });
            Assert.Equal(AccountStorageService.FormatFileSize(2048), result[a1.Id]);
            Assert.Equal(AccountStorageService.FormatFileSize(0), result[a2.Id]);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // RefreshAccountStorageAsync
    // These methods use raw NpgsqlConnection (bypassing EF Core), so they cannot
    // participate in the EF transaction used for test isolation. Each test creates
    // its own non-transactional context and cleans up its rows at the end.
    // ============================================================

    [Fact]
    public async Task RefreshAccountStorageAsync_UpsertsCache()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.ArchivedEmails.Add(new ArchivedEmail
            {
                MailAccountId = acct.Id,
                MessageId = Guid.NewGuid().ToString(),
                Subject = "storage-test",
                From = "a@x.com",
                To = "b@x.com",
                Cc = string.Empty,
                Bcc = string.Empty,
                Body = "some body text that takes a few bytes",
                HtmlBody = "<p>html</p>",
                SentDate = DateTime.UtcNow.AddDays(-1),
                ReceivedDate = DateTime.UtcNow,
                IsOutgoing = false,
                HasAttachments = false,
                FolderName = "INBOX"
            });
            await ctx.SaveChangesAsync();

            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.RefreshAccountStorageAsync(acct.Id);

            // Use a fresh context to read the committed cache row.
            await using var readCtx = _fixture.CreateContext();
            var cache = await readCtx.AccountStorageCaches.AsNoTracking().FirstOrDefaultAsync(c => c.MailAccountId == acct.Id);
            Assert.NotNull(cache);
            Assert.True(cache!.TotalBytes > 0);
        }
        finally
        {
            await CleanupAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task RefreshAccountStorageAsync_NoEmails_UpsertsZeroCache()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.RefreshAccountStorageAsync(acct.Id);

            await using var readCtx = _fixture.CreateContext();
            var cache = await readCtx.AccountStorageCaches.AsNoTracking().FirstOrDefaultAsync(c => c.MailAccountId == acct.Id);
            Assert.NotNull(cache);
            Assert.Equal(0, cache!.TotalBytes);
        }
        finally
        {
            await CleanupAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task RefreshAccountStorageAsync_CreatesBackfillStateDone()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.RefreshAccountStorageAsync(acct.Id);

            await using var readCtx = _fixture.CreateContext();
            var state = await readCtx.AccountStorageBackfillStates.AsNoTracking()
                .FirstOrDefaultAsync(s => s.MailAccountId == acct.Id);
            Assert.NotNull(state);
            Assert.Equal("Done", state!.Status);
        }
        finally
        {
            await CleanupAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes all test rows for accounts created in the given context (emails, caches,
    /// backfill states, user-mail-account links, and the account itself).
    /// </summary>
    private static async Task CleanupAccountAsync(MailArchiverDbContext ctx)
    {
        var accountIds = await ctx.MailAccounts.AsNoTracking()
            .Where(a => a.EmailAddress.EndsWith("@test.local"))
            .Select(a => a.Id)
            .ToListAsync();

        if (accountIds.Count == 0) return;

        var emails = await ctx.ArchivedEmails.Where(e => accountIds.Contains(e.MailAccountId)).ToListAsync();
        ctx.ArchivedEmails.RemoveRange(emails);

        var caches = await ctx.AccountStorageCaches.Where(c => accountIds.Contains(c.MailAccountId)).ToListAsync();
        ctx.AccountStorageCaches.RemoveRange(caches);

        var states = await ctx.AccountStorageBackfillStates.Where(s => accountIds.Contains(s.MailAccountId)).ToListAsync();
        ctx.AccountStorageBackfillStates.RemoveRange(states);

        var umas = await ctx.UserMailAccounts.Where(uma => accountIds.Contains(uma.MailAccountId)).ToListAsync();
        ctx.UserMailAccounts.RemoveRange(umas);

        var accts = await ctx.MailAccounts.Where(a => accountIds.Contains(a.Id)).ToListAsync();
        ctx.MailAccounts.RemoveRange(accts);

        try { await ctx.SaveChangesAsync(); }
        catch { /* best-effort cleanup */ }
    }

    // ============================================================
    // EnsureBackfillStatesAsync
    // ============================================================

    [Fact]
    public async Task EnsureBackfillStatesAsync_CreatesMissing()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.EnsureBackfillStatesAsync();
            var state = await ctx.AccountStorageBackfillStates.AsNoTracking()
                .FirstOrDefaultAsync(s => s.MailAccountId == acct.Id);
            Assert.NotNull(state);
            Assert.Equal("Pending", state!.Status);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task EnsureBackfillStatesAsync_NoMissing_NoOp()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            ctx.AccountStorageBackfillStates.Add(new AccountStorageBackfillState
            {
                MailAccountId = acct.Id,
                Status = "Pending"
            });
            await ctx.SaveChangesAsync();

            // Count only our test account's backfill states (avoids interference from real DB rows).
            var beforeCount = await ctx.AccountStorageBackfillStates.AsNoTracking()
                .CountAsync(s => s.MailAccountId == acct.Id);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.EnsureBackfillStatesAsync();
            var afterCount = await ctx.AccountStorageBackfillStates.AsNoTracking()
                .CountAsync(s => s.MailAccountId == acct.Id);
            Assert.Equal(beforeCount, afterCount);
        }
        finally { await scope.RollbackAsync(); }
    }

    [Fact]
    public async Task EnsureBackfillStatesAsync_Idempotent()
    {
        await using var scope = await _fixture.CreateTransactionalContextAsync();
        var ctx = scope.Context;
        try
        {
            var acct = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.EnsureBackfillStatesAsync();
            await svc.EnsureBackfillStatesAsync();
            var count = await ctx.AccountStorageBackfillStates.AsNoTracking()
                .CountAsync(s => s.MailAccountId == acct.Id);
            Assert.Equal(1, count);
        }
        finally { await scope.RollbackAsync(); }
    }

    // ============================================================
    // RefreshAllAccountStorageAsync
    // ============================================================

    [Fact]
    public async Task RefreshAllAccountStorageAsync_RefreshsAllAccounts()
    {
        var ctx = _fixture.CreateContext();
        try
        {
            var a1 = await SeedAccountAsync(ctx);
            var a2 = await SeedAccountAsync(ctx);
            var svc = ServiceFactory.CreateAccountStorageService(ctx);
            await svc.RefreshAllAccountStorageAsync(CancellationToken.None);

            await using var readCtx = _fixture.CreateContext();
            Assert.True(await readCtx.AccountStorageCaches.AsNoTracking().AnyAsync(c => c.MailAccountId == a1.Id));
            Assert.True(await readCtx.AccountStorageCaches.AsNoTracking().AnyAsync(c => c.MailAccountId == a2.Id));
        }
        finally
        {
            await CleanupAccountAsync(ctx);
            await ctx.DisposeAsync();
        }
    }
}